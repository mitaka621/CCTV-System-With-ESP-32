using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Dtos.CameraFrameDtos;
using Homewatch.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;

namespace Homewatch.Core.Services.Devices
{
    public class CameraFrameStamper : IAsyncDisposable
    {
        private const int _queuedFramesCapacity = 4;
        private const string _originalResolutionQuality = "4";
        private const string _reducedResolutionQuality = "7";

        private static readonly TimeSpan _exitTimeout = TimeSpan.FromSeconds(2);
        private static readonly byte[] _partTrailer = Encoding.ASCII.GetBytes("\r\n");

        private readonly ICameraFramesManagerService _cameraFramesManagerService;
        private readonly ILogger _logger;
        private readonly Channel<byte[]> _queuedFrames;
        private readonly CancellationTokenSource _stopTokenSource = new();
        private readonly NamedPipeServerStream _reducedResolutionPipe;
        private readonly string _reducedResolutionPipeName;
        private readonly Process _ffmpeg;
        private readonly Task _processingTask;
        private long _lastFrameReceivedTicks;
        private int _hasExited;
        private int _isDisposed;

        public CameraFrameStamper(
            CameraFrameStampSettingsDto settings,
            ICameraFramesManagerService cameraFramesManagerService,
            ILogger logger)
        {
            Settings = settings;
            _cameraFramesManagerService = cameraFramesManagerService;
            _logger = logger;
            _lastFrameReceivedTicks = DateTime.UtcNow.Ticks;

            _queuedFrames = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(_queuedFramesCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });

            // on Linux .NET named pipes are Unix domain sockets, and a rooted name is used as the socket path
            _reducedResolutionPipeName = OperatingSystem.IsWindows()
                ? $"homewatch-preview-{settings.CameraId:N}-{Guid.NewGuid():N}"
                : Path.Combine(Path.GetTempPath(), $"homewatch-preview-{Guid.NewGuid():N}.sock");

            var reducedResolutionOutput = OperatingSystem.IsWindows()
                ? $@"\\.\pipe\{_reducedResolutionPipeName}"
                : $"unix:{_reducedResolutionPipeName}";

            _reducedResolutionPipe = new NamedPipeServerStream(
                _reducedResolutionPipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            _ffmpeg = new Process
            {
                StartInfo = CreateStartInfo(settings, reducedResolutionOutput),
                EnableRaisingEvents = true
            };

            _ffmpeg.Exited += OnFfmpegExited;

            try
            {
                _ffmpeg.Start();
            }
            catch
            {
                _reducedResolutionPipe.Dispose();
                _ffmpeg.Dispose();
                throw;
            }

            var stopToken = _stopTokenSource.Token;

            _processingTask = Task.WhenAll(
                WriteFramesAsync(stopToken),
                ReadOriginalResolutionFramesAsync(stopToken),
                ReadReducedResolutionFramesAsync(stopToken),
                LogFfmpegErrorsAsync(stopToken));
        }

        public CameraFrameStampSettingsDto Settings { get; }

        public int ProcessId => _ffmpeg.Id;

        public DateTime LastFrameReceivedUtc => new(Volatile.Read(ref _lastFrameReceivedTicks), DateTimeKind.Utc);

        public bool HasExited => Volatile.Read(ref _hasExited) == 1;

        public void EnqueueFrame(byte[] frame)
        {
            Volatile.Write(ref _lastFrameReceivedTicks, DateTime.UtcNow.Ticks);

            _queuedFrames.Writer.TryWrite(frame);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            {
                return;
            }

            _queuedFrames.Writer.TryComplete();

            if (!await WaitForProcessingAsync() && !HasExited)
            {
                _logger.LogWarning("Frame stamping ffmpeg for camera {CameraId} did not exit in time and is being terminated.", Settings.CameraId);
            }

            TryKillFfmpeg();

            await _stopTokenSource.CancelAsync();
            await WaitForProcessingAsync();

            _reducedResolutionPipe.Dispose();
            _ffmpeg.Dispose();
            _stopTokenSource.Dispose();

            if (!OperatingSystem.IsWindows())
            {
                TryDeleteFile(_reducedResolutionPipeName);
            }
        }

        private static ProcessStartInfo CreateStartInfo(CameraFrameStampSettingsDto settings, string reducedResolutionOutput)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = VideoChunkUtilities.GetFfmpegPath(),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string[] arguments =
            [
                "-hide_banner", "-nostats", "-loglevel", "error", "-y",
                "-filter_threads", "1", "-filter_complex_threads", "1",
                "-threads", "1", "-probesize", "32", "-analyzeduration", "0",
                "-f", "mpjpeg", "-i", "pipe:0",
                "-filter_complex", CameraFrameStampFilterBuilder.Build(settings),
                "-map", $"[{CameraFrameStampFilterBuilder.OriginalResolutionOutputLabel}]",
                "-c:v", "mjpeg", "-q:v", _originalResolutionQuality, "-threads", "1",
                "-fps_mode", "passthrough", "-flush_packets", "1", "-f", "mpjpeg", "pipe:1",
                "-map", $"[{CameraFrameStampFilterBuilder.ReducedResolutionOutputLabel}]",
                "-c:v", "mjpeg", "-q:v", _reducedResolutionQuality, "-threads", "1",
                "-fps_mode", "passthrough", "-flush_packets", "1", "-f", "mpjpeg", reducedResolutionOutput
            ];

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.Environment["TZ"] = CreateFixedOffsetTimeZone(settings.UtcOffsetSeconds);

            return startInfo;
        }

        // the C runtime does not understand IANA names and falls back to UTC, while a POSIX fixed offset works on Windows and Linux
        private static string CreateFixedOffsetTimeZone(int utcOffsetSeconds)
        {
            var sign = utcOffsetSeconds >= 0 ? "-" : "+";
            var offset = TimeSpan.FromSeconds(Math.Abs(utcOffsetSeconds));

            return $"CAM{sign}{offset.Hours:00}:{offset.Minutes:00}:{offset.Seconds:00}";
        }

        private async Task WriteFramesAsync(CancellationToken stopToken)
        {
            var input = _ffmpeg.StandardInput.BaseStream;

            try
            {
                await foreach (var frame in _queuedFrames.Reader.ReadAllAsync(stopToken))
                {
                    var header = Encoding.ASCII.GetBytes($"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {frame.Length}\r\n\r\n");

                    await input.WriteAsync(header, stopToken);
                    await input.WriteAsync(frame, stopToken);
                    await input.WriteAsync(_partTrailer, stopToken);
                    await input.FlushAsync(stopToken);
                }
            }
            catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
            {
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Frame stamping ffmpeg for camera {CameraId} stopped accepting frames.", Settings.CameraId);
            }
            finally
            {
                try
                {
                    input.Close();
                }
                catch (IOException)
                {
                }
            }
        }

        private async Task ReadOriginalResolutionFramesAsync(CancellationToken stopToken)
        {
            await ReadFramesAsync(_ffmpeg.StandardOutput.BaseStream, _cameraFramesManagerService.PublishOriginalResolutionFrame, stopToken);
        }

        private async Task ReadReducedResolutionFramesAsync(CancellationToken stopToken)
        {
            try
            {
                await _reducedResolutionPipe.WaitForConnectionAsync(stopToken);
            }
            catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
            {
                return;
            }

            await ReadFramesAsync(_reducedResolutionPipe, _cameraFramesManagerService.PublishReducedResolutionFrame, stopToken);
        }

        private async Task ReadFramesAsync(Stream stream, Action<Guid, byte[]> publishFrame, CancellationToken stopToken)
        {
            var reader = new MultipartJpegStreamReader(stream);

            try
            {
                while (await reader.ReadFrameAsync(stopToken) is { } frame)
                {
                    try
                    {
                        publishFrame(Settings.CameraId, frame);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to publish a stamped frame for camera {CameraId}.", Settings.CameraId);
                    }
                }
            }
            catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ObjectDisposedException)
            {
                _logger.LogWarning(ex, "Stopped reading stamped frames for camera {CameraId}.", Settings.CameraId);
            }
        }

        private async Task LogFfmpegErrorsAsync(CancellationToken stopToken)
        {
            try
            {
                while (await _ffmpeg.StandardError.ReadLineAsync(stopToken) is { } line)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        _logger.LogWarning("Frame stamping ffmpeg for camera {CameraId}: {Message}", Settings.CameraId, line);
                    }
                }
            }
            catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
            }
        }

        private void OnFfmpegExited(object? sender, EventArgs e)
        {
            Volatile.Write(ref _hasExited, 1);

            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            try
            {
                _logger.LogWarning(
                    "Frame stamping ffmpeg for camera {CameraId} exited unexpectedly with code {ExitCode}.",
                    Settings.CameraId,
                    _ffmpeg.ExitCode);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private async Task<bool> WaitForProcessingAsync()
        {
            try
            {
                await _processingTask.WaitAsync(_exitTimeout);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frame stamping for camera {CameraId} stopped with an error.", Settings.CameraId);
                return true;
            }
        }

        private void TryKillFfmpeg()
        {
            try
            {
                if (!_ffmpeg.HasExited)
                {
                    _ffmpeg.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete the frame stamping socket {Path}.", path);
            }
        }
    }
}
