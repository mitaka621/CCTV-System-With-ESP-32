using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.VideoChunkDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace CamPortal.Core.BackgroundServices
{
    public class VideoEncoderService : BackgroundService
    {
        private readonly ICameraFramesManagerService _framesManager;
        private readonly ILogger<VideoEncoderService> _logger;
        private readonly IDeviceService _cameraService;
        private readonly IVideoReplayService _videoChunkService;
        private readonly ICameraConfigurationRepository _cameraConfigurationRepository;

        private readonly byte[] _placeholderFrame;
        private readonly int _encodedVideoOutputFps;
        private readonly int _videoChunksSizeInS;
        private readonly int _timeoutInS;
        private readonly string _footagePath;
        private readonly VideoHardwareEncoder _activeEncoder;

        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cameraEncodersCancelationSources = new();

        public VideoEncoderService(
            ICameraFramesManagerService framesManager,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ICameraConfigurationRepository cameraConfigurationRepository,
            ILogger<VideoEncoderService> logger)
        {
            _framesManager = framesManager;
            _logger = logger;
            _cameraConfigurationRepository = cameraConfigurationRepository;

            _cameraService = serviceProvider.CreateScope()
                .ServiceProvider
                .GetRequiredService<IDeviceService>();

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

            var requestedEncoder = ParseConfiguredEncoder(configuration);
            _activeEncoder = VideoChunkUtilities.ResolveHardwareEncoder(requestedEncoder, _logger);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _framesManager.ChannelOpened += (Guid cameraId) => OnChannelOpen(cameraId, stoppingToken);

            _framesManager.ChannelClosed += OnChannelClose;

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
            try
            {
                var framesChannel = _framesManager.GetOrCreateProcessedFramesCameraChannel(cameraId);
                DateTime segmentStartTime = DateTime.UtcNow;

                string outputDir = Path.Combine(_footagePath, cameraId.ToString(), DateTime.UtcNow.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(outputDir);

                string tempPattern = Path.Combine(outputDir, $"camera_{cameraId}_%03d.ts");

                var outputResolution = await GetCameraEncodingResolutionAsync(cameraId);

                var ffmpeg = CreateNewFfmpegProccess(tempPattern, outputResolution);

                ffmpeg.Start();

                _logger.LogInformation(
                    "FFmpeg started for camera {CameraId} (PID {Pid}, encoder {Encoder}, output {Width}x{Height}).",
                    cameraId,
                    ffmpeg.Id,
                    _activeEncoder,
                    outputResolution.Width,
                    outputResolution.Height);

                var input = ffmpeg.StandardInput.BaseStream;

                StartBackgroundVideoChunksRenamingJob(ffmpeg, outputDir, cameraId, stoppingToken);

                await EncodeFramesToVideoChunksAsync(framesChannel, input, stoppingToken);

                input.Close();
                await ffmpeg.WaitForExitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "FFmpeg encoding task for camera {CameraId} terminated due to an unhandled exception.",
                    cameraId);
            }
        }

        private string ExtractFileName(string ffmpegLine)
        {
            int start = ffmpegLine.IndexOf("\'") + 1;
            int end = ffmpegLine.LastIndexOf("\'");
            if (start > 0 && end > start)
                return ffmpegLine[start..end].Split("\\").Last();
            return "unknown.mp4";
        }

        private async Task<string> RenameProducedChunkFromFFMPEGAsync(string filename, DateTime dateCreated, string outputDir, Guid cameraId)
        {
            var segmentEndTime = DateTime.UtcNow;

            string newFileName = Path.Combine(
                outputDir,
                $"{cameraId}_={dateCreated:yyyy-MM-dd_HH-mm-ss}__{segmentEndTime:yyyy-MM-dd_HH-mm-ss}=.ts"
            );

            const int maxAttempts = 5;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (File.Exists(newFileName)) File.Delete(newFileName);

                    File.Move(filename, newFileName);
                    _logger.LogInformation(
                        "Renamed video chunk {OldFileName} -> {NewFileName}",
                        filename,
                        newFileName);

                    return newFileName;
                }
                catch (IOException ex) when (attempt < maxAttempts)
                {
                    _logger.LogWarning(
                        ex,
                        "Video chunk {FileName} is still locked, retry {Attempt}/{MaxAttempts}",
                        filename,
                        attempt,
                        maxAttempts);

                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to rename {FileName}", filename);
                    return newFileName;
                }
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

                const int maxCapturedStderrLines = 50;
                var recentStderrLines = new Queue<string>(maxCapturedStderrLines);

                while ((line = await reader.ReadLineAsync()) != null && !stoppingToken.IsCancellationRequested)
                {
                    if (recentStderrLines.Count >= maxCapturedStderrLines)
                        recentStderrLines.Dequeue();

                    recentStderrLines.Enqueue(line);

                    if (line.Contains("Opening") && line.Contains(cameraId.ToString()))
                    {
                        // ffmpeg line: Opening 'camera_xxx_001.mp4' for writing
                        var rawFileName = ExtractFileName(line);
                        var currentFile = Path.Combine(outputDir, rawFileName);

                        if (lastFileName != null && lastFileStartTime != null)
                        {
                            var fileName = await RenameProducedChunkFromFFMPEGAsync(lastFileName, lastFileStartTime.Value, outputDir, cameraId);

                            lastFileDto = new CreateVideoChunkDto
                            {
                                DeviceId = cameraId,
                                ChunkStartTime = lastFileStartTime.Value,
                                ChunkEndTime = DateTime.UtcNow,
                                FileName = fileName,
                                SizeInMB = Math.Round(new FileInfo(fileName).Length / (1024.0 * 1024.0), 2)
                            };

                            await _videoChunkService.SaveVideoChunkInfoAsync(lastFileDto!);
                        }

                        lastFileName = currentFile;
                        lastFileStartTime = DateTime.UtcNow;
                    }
                }

                bool cancellationRequested = stoppingToken.IsCancellationRequested;

                if (!ffmpeg.HasExited)
                {
                    try { ffmpeg.WaitForExit(5000); } catch { }
                }

                LogFfmpegExit(cameraId, ffmpeg, recentStderrLines, cancellationRequested);

                if (!ffmpeg.HasExited)
                {
                    try { ffmpeg.Kill(); } catch { }
                }

                try { ffmpeg.WaitForExit(2000); } catch { }

                if (lastFileName != null)
                {
                    var startTime = File.GetCreationTime(lastFileName);

                    var fileName = await RenameProducedChunkFromFFMPEGAsync(lastFileName, startTime, outputDir, cameraId);

                    if (lastFileDto != null)
                    {
                        await _videoChunkService.SaveVideoChunkInfoAsync(lastFileDto);
                    }
                }

            }, stoppingToken);
        }

        private void LogFfmpegExit(Guid cameraId, Process ffmpeg, IEnumerable<string> recentStderrLines, bool cancellationRequested)
        {
            int? exitCode = null;
            try
            {
                if (ffmpeg.HasExited)
                    exitCode = ffmpeg.ExitCode;
            }
            catch { }

            if (cancellationRequested)
            {
                _logger.LogInformation(
                    "FFmpeg for camera {CameraId} stopped because its channel was cancelled (exit code {ExitCode}).",
                    cameraId,
                    exitCode);

                return;
            }

            const int maxStderrTailLength = 3500;

            var stderrTail = string.Join(Environment.NewLine, recentStderrLines);

            if (stderrTail.Length > maxStderrTailLength)
                stderrTail = "...(truncated)..." + stderrTail[^maxStderrTailLength..];

            _logger.LogError(
                "FFmpeg for camera {CameraId} exited unexpectedly with code {ExitCode}. Recent FFmpeg output:{NewLine}{StderrTail}",
                cameraId,
                exitCode,
                Environment.NewLine,
                string.IsNullOrWhiteSpace(stderrTail) ? "(no output captured)" : stderrTail);
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

                _cameraEncodersCancelationSources.TryAdd(cameraId, perCameraCts);

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

            _cameraEncodersCancelationSources.TryRemove(cameraId, out _);

            _logger.LogInformation(
                "Channel closed for camera {CameraId}. Cancelling video encoding task.",
                cameraId);
        }

        private async Task<(int Width, int Height)> GetCameraEncodingResolutionAsync(Guid cameraId)
        {
            try
            {
                var configuration = await _cameraConfigurationRepository.GetCameraConfigurationAsync(cameraId);

                if (configuration == null)
                {
                    return (0, 0);
                }

                return CameraAspectRatioResolver.GetEncodingResolution(configuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve encoding resolution for camera {CameraId}", cameraId);
                return (0, 0);
            }
        }

        private Process CreateNewFfmpegProccess(string filePath, (int Width, int Height) outputResolution)
        {
            var ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = VideoChunkUtilities.GetFfmpegPath(),
                    Arguments = BuildFfmpegArguments(filePath, outputResolution),
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            return ffmpeg;
        }

        private string BuildFfmpegArguments(string filePath, (int Width, int Height) outputResolution)
        {
            int fps = _encodedVideoOutputFps;
            int gop = fps * _videoChunksSizeInS;

            string inputSection =
                $"-f mjpeg -framerate {fps} -i pipe:0 -map 0:v:0 -an ";

            string filterSection = outputResolution.Width > 0 && outputResolution.Height > 0
                ? $"-vf scale={outputResolution.Width}:{outputResolution.Height}:force_original_aspect_ratio=decrease,pad={outputResolution.Width}:{outputResolution.Height}:(ow-iw)/2:(oh-ih)/2,setsar=1 "
                : string.Empty;

            string codecSection = _activeEncoder switch
            {
                VideoHardwareEncoder.Nvidia =>
                    $"-c:v h264_nvenc -preset p4 -tune ll -rc cbr -no-scenecut 1 -g {gop} -keyint_min {gop} ",
                VideoHardwareEncoder.Intel =>
                    $"-c:v h264_qsv -preset veryfast -look_ahead 0 -g {gop} ",
                VideoHardwareEncoder.Amd =>
                    $"-c:v h264_amf -quality balanced -rc cbr -g {gop} ",
                _ =>
                    $"-c:v libx264 -preset veryfast -tune zerolatency -sc_threshold 0 -threads 2 -g {gop} -keyint_min {gop} "
            };

            string rateSection =
                $"-pix_fmt yuv420p -r {fps} -b:v 1500k -maxrate 2000k -bufsize 4000k ";

            string outputSection =
                $"-f segment -segment_time {_videoChunksSizeInS} -segment_format mpegts {filePath}";

            return inputSection + filterSection + codecSection + rateSection + outputSection;
        }

        private static VideoHardwareEncoder ParseConfiguredEncoder(IConfiguration configuration)
        {
            var raw = configuration.GetSection("VideoEncoderConfig")["HardwareEncoder"];
            if (string.IsNullOrWhiteSpace(raw))
            {
                return VideoHardwareEncoder.Auto;
            }

            if (Enum.TryParse<VideoHardwareEncoder>(raw, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return VideoHardwareEncoder.Auto;
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
