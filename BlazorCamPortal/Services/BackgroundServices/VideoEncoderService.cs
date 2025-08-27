using System.Diagnostics;
using System.Threading.Channels;
using BlazorCamPortal.Contracts.Abstractions.Services;
using BlazorCamPortal.Contracts.Dtos.VideoChunkDtos;
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
        private readonly IVideoReplayService _videoChunkService;

        private readonly byte[] _placeholderFrame;
        private readonly int _encodedVideoOutputFps;
        private readonly int _videoChunksSizeInS;
        private readonly int _timeoutInS;
        private readonly string _footagePath;

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

            _videoChunkService = serviceProvider.CreateScope()
                .ServiceProvider
                .GetRequiredService<IVideoReplayService>(); ;

            _placeholderFrame = VideoChunkUtilities.GetDefaultFrame(configuration);

            _timeoutInS = int.Parse(
                configuration.GetSection("ESPCamera")["CameraTimeoutInSeconds"]
                ?? throw new ArgumentNullException("Video encoder timeout not configured")
            );

            _encodedVideoOutputFps = int.Parse(
                configuration.GetSection("VideoEncoderConfig")["EncodedVideoOutputFps"]
                ?? throw new ArgumentNullException("Encoded video output FPS not configured")
            );

            _videoChunksSizeInS = int.Parse(
                configuration.GetSection("VideoEncoderConfig")["VideoChunksSizeInS"]
                ?? throw new ArgumentNullException("Video chunk size not configured")
            );

            _footagePath = configuration.GetSection("VideoEncoderConfig")["VideoChunksFolder"]
                ?? throw new ArgumentNullException("VideoChunksFolder not configured");
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

            string outputDir = Path.Combine(_footagePath, cameraId.ToString(), DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(outputDir);

            string tempPattern = Path.Combine(outputDir, $"camera_{cameraId}_%03d.ts");

            var ffmpeg = CreateNewFfmpegProccess(tempPattern);

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

        private string RenameProducedChunkFromFFMPEG(string filename, DateTime dateCreated, string outputDir, Guid cameraId)
        {
            var segmentEndTime = DateTime.Now;

            string newFileName = Path.Combine(
                outputDir,
                $"{cameraId}_={dateCreated:yyyy-MM-dd_HH-mm-ss}__{segmentEndTime:yyyy-MM-dd_HH-mm-ss}=.ts"
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

            return newFileName;
        }

        private void StartBackgroundVideoChunksRenamingJob(Process ffmpeg, string outputDir, Guid cameraId, CancellationToken stoppingToken)
        {
            _ = Task.Run(async () =>
            {
                using var reader = ffmpeg.StandardError;
                string? line;
                string? lastFileName = null;
                DateTime? lastFileStartTime = null;
                CreateVideoChunkDto? lastFileDto = null;

                while ((line = await reader.ReadLineAsync()) != null && !stoppingToken.IsCancellationRequested)
                {
                    if (line.Contains("Opening") && line.Contains(cameraId.ToString()))
                    {
                        // ffmpeg line: Opening 'camera_xxx_001.mp4' for writing
                        var rawFileName = ExtractFileName(line);
                        var currentFile = Path.Combine(outputDir, rawFileName);

                        if (lastFileName != null && lastFileStartTime != null)
                        {
                            var fileName = RenameProducedChunkFromFFMPEG(lastFileName, lastFileStartTime.Value, outputDir, cameraId);

                            lastFileDto = new CreateVideoChunkDto
                            {
                                CameraId = cameraId,
                                ChunkStartDate = lastFileStartTime.Value,
                                ChunkEndDate = DateTime.Now,
                                FileName = fileName,
                                SizeInMB = Math.Round(new FileInfo(fileName).Length / (1024.0 * 1024.0), 2)
                            };

                            await _videoChunkService.SaveVideoChunkInfoAsync(lastFileDto!);
                        }

                        lastFileName = currentFile;
                        lastFileStartTime = DateTime.Now;
                    }
                }

                // handlining last file on server shutdown
                ffmpeg.Kill();

                if (lastFileName != null)
                {
                    var startTime = File.GetCreationTime(lastFileName);

                    var fileName = RenameProducedChunkFromFFMPEG(lastFileName, startTime, outputDir, cameraId);

                    if (lastFileDto != null)
                    {
                        await _videoChunkService.SaveVideoChunkInfoAsync(lastFileDto);
                    }
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

            _cameraEncodersCancelationSources.Remove(cameraId);

            _logger.LogInformation($"Channel closed for camera {cameraId}. Cancelling video encoding task.");
        }

        private Process CreateNewFfmpegProccess(string filePath)
        {
            var ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = VideoChunkUtilities.GetFfmpegPath(),
                    Arguments =
                        $"-f mjpeg -framerate {_encodedVideoOutputFps} -i pipe:0 " +
                        "-map 0:v:0 -an " +
                        "-c:v libx264 -preset medium -tune zerolatency -sc_threshold 0 " +
                        $"-g {_encodedVideoOutputFps * 2} -keyint_min {_encodedVideoOutputFps * 2} " +
                        $"-pix_fmt yuv420p -r {_encodedVideoOutputFps} " +
                        $"-f segment -segment_time {_videoChunksSizeInS} -segment_format mpegts " +
                        $"{filePath}",
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            return ffmpeg;
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
