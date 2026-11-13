using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Dtos.CameraFrameDtos;
using Homewatch.Contracts.Dtos.DeviceDtos;
using Homewatch.Core.Services.Devices;
using Homewatch.Core.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Homewatch.Core.BackgroundServices
{
    public class RawFrameProcessorService : BackgroundService
    {
        private static readonly TimeSpan _idleStamperTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan _failedStamperRestartDelay = TimeSpan.FromSeconds(5);

        private readonly ICameraFramesManagerService _cameraFramesManagerService;
        private readonly ILogger<RawFrameProcessorService> _logger;
        private readonly IActiveCameraConnections _activeCameraConnections;
        private readonly ConcurrentDictionary<Guid, CameraFrameStamper> _stampersByCamera = new();
        private readonly ConcurrentDictionary<Guid, DateTime> _stamperRestartNotBeforeByCamera = new();

        public RawFrameProcessorService(
            ICameraFramesManagerService cameraFramesManagerService,
            ILogger<RawFrameProcessorService> logger,
            IActiveCameraConnections activeCameraConnections)
        {
            _cameraFramesManagerService = cameraFramesManagerService;
            _logger = logger;
            _activeCameraConnections = activeCameraConnections;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var idleStamperCleanupTask = StopIdleStampersAsync(stoppingToken);

            try
            {
                await DispatchFramesAsync(stoppingToken);
            }
            finally
            {
                await idleStamperCleanupTask;

                await Task.WhenAll(_stampersByCamera.Values.Select(StopStamperAsync));
                _stampersByCamera.Clear();
            }
        }

        private async Task DispatchFramesAsync(CancellationToken stoppingToken)
        {
            var rawFramesReader = _cameraFramesManagerService.RawFramesChannelReader;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var (device, frame) = await rawFramesReader.ReadAsync(stoppingToken);

                    if (!_activeCameraConnections.IsCameraActive(device.Id))
                        continue;

                    GetRunningStamper(device, frame)?.EnqueueFrame(frame);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Error processing raw camera frames"); }
            }
        }

        private CameraFrameStamper? GetRunningStamper(DeviceStreamingHandshakeWithCameraConfigDto device, byte[] frame)
        {
            if (!JpegHeaderReader.TryReadDimensions(frame, out var frameWidth, out var frameHeight))
            {
                _logger.LogWarning("Dropped a frame from camera {CameraId} because its JPEG header could not be read.", device.Id);
                return null;
            }

            var settings = CreateStampSettings(device, frameWidth, frameHeight);

            if (_stampersByCamera.TryGetValue(device.Id, out var currentStamper))
            {
                if (!currentStamper.HasExited && currentStamper.Settings == settings)
                {
                    return currentStamper;
                }

                if (currentStamper.HasExited)
                {
                    _stamperRestartNotBeforeByCamera[device.Id] = DateTime.UtcNow + _failedStamperRestartDelay;
                }

                if (_stampersByCamera.TryRemove(new KeyValuePair<Guid, CameraFrameStamper>(device.Id, currentStamper)))
                {
                    _ = StopStamperAsync(currentStamper);
                }
            }

            if (_stamperRestartNotBeforeByCamera.TryGetValue(device.Id, out var restartNotBefore) && DateTime.UtcNow < restartNotBefore)
            {
                return null;
            }

            try
            {
                var stamper = new CameraFrameStamper(settings, _cameraFramesManagerService, _logger);

                _stampersByCamera[device.Id] = stamper;

                _logger.LogInformation(
                    "Frame stamping ffmpeg started for camera {CameraId} (PID {Pid}, input {Width}x{Height}).",
                    device.Id,
                    stamper.ProcessId,
                    frameWidth,
                    frameHeight);

                return stamper;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start frame stamping ffmpeg for camera {CameraId}.", device.Id);

                _stamperRestartNotBeforeByCamera[device.Id] = DateTime.UtcNow + _failedStamperRestartDelay;

                return null;
            }
        }

        private async Task StopIdleStampersAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_idleStamperTimeout);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    foreach (var (cameraId, stamper) in _stampersByCamera)
                    {
                        if (DateTime.UtcNow - stamper.LastFrameReceivedUtc < _idleStamperTimeout
                            || !_stampersByCamera.TryRemove(new KeyValuePair<Guid, CameraFrameStamper>(cameraId, stamper)))
                        {
                            continue;
                        }

                        _logger.LogInformation("Frame stamping ffmpeg stopped for camera {CameraId} because no frames arrived.", cameraId);

                        await StopStamperAsync(stamper);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }

        private async Task StopStamperAsync(CameraFrameStamper stamper)
        {
            try
            {
                await stamper.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop frame stamping ffmpeg for camera {CameraId}.", stamper.Settings.CameraId);
            }
        }

        private static CameraFrameStampSettingsDto CreateStampSettings(DeviceStreamingHandshakeWithCameraConfigDto device, int frameWidth, int frameHeight)
        {
            var configuration = device.CameraConfiguration;

            return new CameraFrameStampSettingsDto
            {
                CameraId = device.Id,
                CameraName = device.DeviceName ?? $"<No Name Set, Device id {device.Id}>",
                FrameWidth = frameWidth,
                FrameHeight = frameHeight,
                FrameRotation = configuration.FrameRotation,
                ZoomFactor = configuration.ZoomFactor,
                ZoomStartX = configuration.ZoomStartX,
                ZoomStartY = configuration.ZoomStartY,
                Brightness = configuration.Brightness,
                Contrast = configuration.Contrast,
                FlipMode = configuration.FlipMode,
                CameraAspectRatio = configuration.CameraAspectRatio,
                UtcOffsetSeconds = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalSeconds
            };
        }
    }
}
