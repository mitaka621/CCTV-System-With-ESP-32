using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using BlazorCamPortal.Contracts.Abstractions.Services;
using BlazorCamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorCamPortal.Core.BackgroundServices
{
    public class VideoEncoderService : BackgroundService
    {
        private readonly ICameraFramesManagerService _framesManager;
        private readonly ILogger<VideoEncoderService> _logger;
        private readonly ICameraService _cameraService;
        private readonly byte[] _placeholderFrame;
        private readonly int _encodedVideoOutputFps;
        private readonly int _videoChunksSizeInM;
        private readonly int _timeoutInS;

        private readonly Dictionary<Guid, CancellationTokenSource> _cameraEncodersCancelationSources = new();

        public VideoEncoderService(
            ICameraFramesManagerService framesManager,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ILogger<VideoEncoderService> logger)
        {
            _framesManager = framesManager;

            _logger = logger;

            _cameraService = serviceProvider.CreateScope()
                .ServiceProvider
                .GetRequiredService<ICameraService>();

            _placeholderFrame = FrameUtilities.GetDefaultFrame(configuration);

            _timeoutInS = int.Parse(
                configuration.GetSection("ESPCamera")["CameraTimeoutInSeconds"]
                ?? throw new ArgumentNullException("Video encoder timeout not configured")
            );

            _encodedVideoOutputFps = int.Parse(
                configuration.GetSection("VideoEncoderConfig")["EncodedVideoOutputFps"]
                ?? throw new ArgumentNullException("Encoded video output FPS not configured")
            );

            _videoChunksSizeInM = int.Parse(
                configuration.GetSection("VideoEncoderConfig")["VideoChunksSizeInM"]
                ?? throw new ArgumentNullException("Video chunk size not configured")
            );
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _framesManager.ChannelOpened += (Guid cameraId) => OnChannelOpen(cameraId, stoppingToken);

            _framesManager.ChannelClosed += OnChannelClose;

            await _framesManager.InitializeAsync(_cameraService);

            _ = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }, stoppingToken);
        }

        private async Task EncodeCameraFramesAsync(Guid cameraId, CancellationToken stoppingToken)
        {
            var framesChannel = _framesManager.GetOrCreateChannel(cameraId);
            DateTime segmentStartTime = DateTime.Now;

            string outputDir = Path.Combine("footage", cameraId.ToString(), DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(outputDir);

            string tempPattern = Path.Combine(outputDir, $"camera_{cameraId}_%03d.mp4");

            var ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetFfmpegPath(),
                    Arguments =
                        $"-f mjpeg -framerate {_encodedVideoOutputFps} -i pipe:0 " +
                        "-c:v libx264 -preset veryfast -tune zerolatency " +
                        $"-pix_fmt yuv420p -r {_encodedVideoOutputFps} " +
                        $"-f segment -segment_time {_videoChunksSizeInM * 60} -reset_timestamps 1 " +
                        $"{tempPattern}",
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            ffmpeg.Start();
            var input = ffmpeg.StandardInput.BaseStream;

            StartBackgroundVideoChunksRenamingJob(ffmpeg, outputDir, cameraId, stoppingToken);

            await EncodeFramesToVideoChunksAsync(framesChannel, input, stoppingToken);

            input.Close();
            await ffmpeg.WaitForExitAsync(stoppingToken);
        }

        private string ExtractFileName(string ffmpegLine)
        {
            int start = ffmpegLine.IndexOf("\'") + 1;
            int end = ffmpegLine.LastIndexOf("\'");
            if (start > 0 && end > start)
                return ffmpegLine[start..end].Split("\\").Last();
            return "unknown.mp4";
        }

        private string GetFfmpegPath()
        {
            string basePath = AppContext.BaseDirectory;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine(basePath, "ffmpeg", "win-x64", "ffmpeg.exe");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return Path.Combine(basePath, "ffmpeg", "linux-x64", "ffmpeg");

            throw new PlatformNotSupportedException("Unsupported OS for FFmpeg");
        }

        private void RenameProducedChunkFromFFMPEG(string filename, string outputDir, Guid cameraId)
        {
            var segmentEndTime = DateTime.Now;
            var startTime = File.GetCreationTime(filename);

            string newFileName = Path.Combine(
                outputDir,
                $"{cameraId}_={startTime:yyyy-MM-dd_HH-mm-ss}__{segmentEndTime:yyyy-MM-dd_HH-mm-ss}=.mp4"
            );

            try
            {
                if (File.Exists(newFileName)) File.Delete(newFileName);

                File.Move(filename, newFileName);
                _logger.LogInformation($"Renamed: {filename} -> {newFileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to rename {filename}: {ex.Message}");
            }
        }

        private void StartBackgroundVideoChunksRenamingJob(Process ffmpeg, string outputDir, Guid cameraId, CancellationToken stoppingToken)
        {
            _ = Task.Run(async () =>
            {
                using var reader = ffmpeg.StandardError;
                string? line;
                string? lastFileName = null;

                while ((line = await reader.ReadLineAsync()) != null && !stoppingToken.IsCancellationRequested)
                {
                    if (line.Contains("Opening") && line.Contains(cameraId.ToString()))
                    {
                        // ffmpeg line: Opening 'camera_xxx_001.mp4' for writing
                        var rawFileName = ExtractFileName(line);
                        var currentFile = Path.Combine(outputDir, rawFileName);

                        if (lastFileName != null)
                        {
                            RenameProducedChunkFromFFMPEG(lastFileName, outputDir, cameraId);
                        }

                        lastFileName = currentFile;
                    }
                }

                // handlining last file on server shutdown
                ffmpeg.Kill();

                if (lastFileName != null)
                {
                    RenameProducedChunkFromFFMPEG(lastFileName, outputDir, cameraId);
                }

            }, stoppingToken);
        }

        private async Task EncodeFramesToVideoChunksAsync(Channel<byte[]> framesChannel, Stream input, CancellationToken stoppingToken)
        {
            TimeSpan frameInterval = TimeSpan.FromSeconds(1.0 / _encodedVideoOutputFps);
            DateTime lastFrameTime = DateTime.MinValue;
            long frameCount = 0;
            byte[]? lastFrame = null;

            var sw = new Stopwatch();
            sw.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                var targetTime = TimeSpan.FromTicks(frameInterval.Ticks * frameCount);

                byte[]? frame = null;
                if (framesChannel.Reader.TryRead(out var newFrame))
                {
                    lastFrame = newFrame;
                    lastFrameTime = DateTime.UtcNow;
                    frame = newFrame;
                }
                else if (lastFrame != null)
                {
                    if ((DateTime.UtcNow - lastFrameTime).TotalSeconds <= _timeoutInS)
                    {
                        frame = lastFrame;
                    }
                    else
                    {
                        frame = _placeholderFrame;
                    }
                }
                else
                {
                    //if initially the channel is empty (and we do not have prev frame) we use the placeholder frame
                    frame = _placeholderFrame;
                    lastFrameTime = DateTime.UtcNow;
                }

                try
                {
                    await input.WriteAsync(frame, 0, frame.Length, stoppingToken);
                }
                catch (TaskCanceledException) { }
                catch (ObjectDisposedException) { }

                frameCount++;

                var delay = targetTime - sw.Elapsed;
                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (TaskCanceledException) { }
                }
            }
        }

        private void OnChannelOpen(Guid cameraId, CancellationToken stoppingToken)
        {
            try
            {
                var perCameraCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                _cameraEncodersCancelationSources.Add(cameraId, perCameraCts);

                _ = Task.Run(() => EncodeCameraFramesAsync(cameraId, perCameraCts.Token), perCameraCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting client");
            }
        }

        private void OnChannelClose(Guid cameraId)
        {
            _cameraEncodersCancelationSources.GetValueOrDefault(cameraId)?.Cancel();

            _logger.LogInformation($"Channel closed for camera {cameraId}. Cancelling video encoding task.");
        }

        public override void Dispose()
        {
            foreach (var cts in _cameraEncodersCancelationSources.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }

            _framesManager.ChannelClosed -= OnChannelClose;

            base.Dispose();
        }
    }
}
