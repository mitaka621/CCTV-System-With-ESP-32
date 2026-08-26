using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.SecurityDtos;
using CamPortal.Contracts.Dtos.TelemetryDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Exceptions;
using CamPortal.Core.Services.Telemetry;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Channels;

namespace CamPortal.Core.Services.DeviceSessionHandlers
{
    public class CameraSessionHandler : IDeviceSessionHandler, ICameraCommandDispatcher
    {
        private const int _resolutionHeaderLen = 8;
        private const int _telemetryLengthPrefixLen = 2;
        private const int _telemetryHeaderLen = _resolutionHeaderLen + _telemetryLengthPrefixLen;
        private const int _telemetryPayloadLen = 44;
        private const byte _telemetryVersion = 2;
        private const int _commandQueueCapacity = 32;
        private static readonly TimeSpan _telemetrySampleInterval = TimeSpan.FromSeconds(20);

        private readonly ConcurrentDictionary<Guid, Channel<OutboundDeviceMessageDto>> _commandChannels = new();

        private readonly ILogger<CameraSessionHandler> _logger;
        private readonly IActiveCameraConnections _activeCameraConnections;
        private readonly ICameraFramesManagerService _cameraFramesManagerService;
        private readonly ICameraConfigurationService _cameraConfigurationService;
        private readonly ICameraConfigurationRepository _cameraConfigurationRepository;
        private readonly CameraTelemetryQueue _telemetryQueue;
        private readonly ICameraSecurityCoordinator _securityCoordinator;
        private readonly IMapper _mapper;
        private readonly int _frameReadTimeoutSeconds;
        private readonly int _maxSessionDurationMinutes;
        private readonly long _maxSessionFrames;

        public CameraSessionHandler(
            ILogger<CameraSessionHandler> logger,
            IActiveCameraConnections activeCameraConnections,
            ICameraFramesManagerService cameraFramesManagerService,
            ICameraConfigurationService cameraConfigurationService,
            CameraTelemetryQueue telemetryQueue,
            ICameraSecurityCoordinator securityCoordinator,
            IConfiguration configuration,
            IMapper mapper,
            ICameraConfigurationRepository cameraConfigurationRepository)
        {
            _logger = logger;
            _activeCameraConnections = activeCameraConnections;
            _cameraFramesManagerService = cameraFramesManagerService;
            _cameraConfigurationService = cameraConfigurationService;
            _telemetryQueue = telemetryQueue;
            _securityCoordinator = securityCoordinator;
            _mapper = mapper;
            _cameraConfigurationRepository = cameraConfigurationRepository;

            var streamingSection = configuration.GetSection("SecureStreaming");

            _frameReadTimeoutSeconds = int.Parse(
                streamingSection["FrameReadTimeoutSeconds"]
                ?? throw new ArgumentNullException("Frame read timeout not configured"));

            _maxSessionDurationMinutes = int.Parse(
                streamingSection["MaxSessionDurationMinutes"]
                ?? throw new ArgumentNullException("Max session duration not configured"));

            _maxSessionFrames = long.Parse(
                streamingSection["MaxSessionFrames"]
                ?? throw new ArgumentNullException("Max session frames not configured"));
        }

        public DeviceTypeCategories DeviceCategory => DeviceTypeCategories.Camera;

        public async Task RunRecieveLoopAsync(ISecureChannel secureChannel, DeviceStreamingHandshakeDto device, CancellationToken cancellationToken)
        {
            var sessionToken = _activeCameraConnections.Register(device.Id, cancellationToken);

            try
            {
                var config = await _cameraConfigurationService.GetCameraConfigurationAsync(device.Id);

                var deviceWithConfigDto = _mapper.Map<DeviceStreamingHandshakeWithCameraConfigDto>(device);

                deviceWithConfigDto.CameraConfiguration = await _cameraConfigurationRepository.GetCameraConfigurationAsync(device.Id)
                    ?? throw new InvalidOperationException("Camera configuration not found");

                try
                {
                    await _securityCoordinator.OnCameraConnectedAsync(device.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Stream {DeviceId}: security coordinator connect hook failed", device.Id);
                }

                var sessionExpiresAt = DateTime.UtcNow.AddMinutes(_maxSessionDurationMinutes);
                ulong totalFrames = 0;
                var resolutionSaved = false;
                var lastTelemetrySampleUtc = DateTime.MinValue;

                while (!sessionToken.IsCancellationRequested)
                {
                    if (DateTime.UtcNow >= sessionExpiresAt)
                    {
                        _logger.LogInformation("Stream {DeviceId}: session expired by time", device.Id);
                        return;
                    }

                    if (totalFrames >= (ulong)_maxSessionFrames)
                    {
                        _logger.LogInformation("Stream {DeviceId}: session expired by frame count", device.Id);
                        return;
                    }

                    using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
                    idleCts.CancelAfter(TimeSpan.FromSeconds(_frameReadTimeoutSeconds));

                    byte[] plaintext;
                    try
                    {
                        plaintext = await secureChannel.ReceiveAsync(idleCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (sessionToken.IsCancellationRequested)
                        {
                            return;
                        }
                        _logger.LogWarning("Stream {DeviceId}: idle timeout", device.Id);
                        return;
                    }
                    catch (SecureChannelProtocolException ex)
                    {
                        _logger.LogWarning("Stream {DeviceId}: {Reason}", device.Id, ex.Message);
                        return;
                    }
                    catch (CryptographicException ex)
                    {
                        _logger.LogWarning(ex, "Stream {DeviceId}: frame decryption failed", device.Id);
                        return;
                    }
                    catch (EndOfStreamException)
                    {
                        return;
                    }

                    if (plaintext.Length < _telemetryHeaderLen)
                    {
                        _logger.LogWarning("Stream {DeviceId}: frame too small ({Length} bytes)", device.Id, plaintext.Length);
                        return;
                    }

                    var telemetryLen = BinaryPrimitives.ReadUInt16BigEndian(plaintext.AsSpan(_resolutionHeaderLen, _telemetryLengthPrefixLen));
                    var jpegOffset = _telemetryHeaderLen + telemetryLen;

                    if (plaintext.Length < jpegOffset + 1)
                    {
                        _logger.LogWarning("Stream {DeviceId}: frame too small ({Length} bytes)", device.Id, plaintext.Length);
                        return;
                    }

                    totalFrames++;

                    if (!resolutionSaved)
                    {
                        resolutionSaved = true;

                        var resolutionWidth = (int)BinaryPrimitives.ReadUInt32BigEndian(plaintext.AsSpan(0, 4));
                        var resolutionHeight = (int)BinaryPrimitives.ReadUInt32BigEndian(plaintext.AsSpan(4, 4));

                        await _cameraConfigurationService.SetCameraResolutionAsync(new CameraResolutionDto()
                        {
                            CameraId = device.Id,
                            Width = resolutionWidth,
                            Height = resolutionHeight
                        });
                    }

                    var now = DateTime.UtcNow;

                    var telemetrySample = DecodeTelemetry(plaintext.AsSpan(_telemetryHeaderLen, telemetryLen), device.Id, now);

                    if (telemetrySample != null)
                    {
                        _securityCoordinator.Ingest(telemetrySample);

                        if (telemetryLen > 0 && now - lastTelemetrySampleUtc >= _telemetrySampleInterval)
                        {
                            _telemetryQueue.Writer.TryWrite(telemetrySample);

                            lastTelemetrySampleUtc = now;
                        }
                    }

                    var jpeg = plaintext.AsSpan(jpegOffset).ToArray();

                    _cameraFramesManagerService.AddFrame(deviceWithConfigDto, jpeg);
                }
            }
            finally
            {
                _securityCoordinator.OnCameraDisconnected(device.Id);
                _activeCameraConnections.TryDisconnect(device.Id);
                _cameraFramesManagerService.CloseProcessedFramesCameraChannel(device.Id);
            }
        }

        public async Task RunSendLoopAsync(ISecureChannel secureChannel, DeviceStreamingHandshakeDto device, CancellationToken cancellationToken)
        {
            var channel = GetOrCreateCommandChannel(device.Id);

            try
            {
                await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    if (!DeviceSessionHelper.TryBuildPayload(message, out var payload))
                    {
                        continue;
                    }

                    try
                    {
                        await secureChannel.SendAsync(payload, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        channel.Writer.TryWrite(message);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Stream {DeviceId}: failed to send outbound message", device.Id);
                        channel.Writer.TryWrite(message);
                        return;
                    }

                    _logger.LogInformation("Stream {DeviceId}: sent command {Command}", device.Id, message.Command);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        public bool TryEnqueueCommand(Guid cameraId, DeviceCommand command)
        {
            return GetOrCreateCommandChannel(cameraId).Writer.TryWrite(new OutboundDeviceMessageDto { Command = command });
        }

        public bool TryEnqueueConfig(Guid cameraId, DeviceEspConfigDto config)
        {
            return GetOrCreateCommandChannel(cameraId).Writer.TryWrite(new OutboundDeviceMessageDto { Command = DeviceCommand.SaveNewConfig, Config = config });
        }

        public void RemoveCamera(Guid cameraId)
        {
            if (_commandChannels.TryRemove(cameraId, out var channel))
            {
                channel.Writer.TryComplete();
            }
        }

        private Channel<OutboundDeviceMessageDto> GetOrCreateCommandChannel(Guid cameraId)
        {
            return _commandChannels.GetOrAdd(cameraId, _ => Channel.CreateBounded<OutboundDeviceMessageDto>(new BoundedChannelOptions(_commandQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false,
            }));
        }

        private CameraTelemetrySampleDto? DecodeTelemetry(ReadOnlySpan<byte> payload, Guid cameraId, DateTime timestampUtc)
        {
            if (payload.Length < _telemetryPayloadLen || payload[0] != _telemetryVersion)
            {
                return null;
            }

            var flags = payload[34];
            var sensorFlags = payload[41];
            var statusFlags = payload[42];
            var eventMask = payload[43];

            return new CameraTelemetrySampleDto
            {
                CameraId = cameraId,
                TimestampUtc = timestampUtc,
                Fps = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(1, 2)) / 10.0,
                AvgCaptureMs = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(3, 2)),
                MaxCaptureMs = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(5, 2)),
                AvgEncryptMs = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(7, 2)),
                MaxEncryptMs = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(9, 2)),
                AvgSendMs = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(11, 2)),
                MaxSendMs = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(13, 2)),
                AvgFrameKB = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(15, 2)),
                MaxFrameKB = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(17, 2)),
                BufferReadyPercent = payload[19],
                FrameCount = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(20, 4)),
                FailedSends = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(24, 4)),
                CaptureFailures = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(28, 4)),
                LightSensorValue = BinaryPrimitives.ReadInt16BigEndian(payload.Slice(32, 2)),
                IsNight = (flags & 0x01) != 0,
                LightSensorPresent = (flags & 0x02) != 0,
                TemperatureC = BinaryPrimitives.ReadInt16BigEndian(payload.Slice(35, 2)) / 100.0,
                HumidityPercent = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(37, 2)) / 100.0,
                DewPointC = BinaryPrimitives.ReadInt16BigEndian(payload.Slice(39, 2)) / 100.0,
                TempHumiditySensorPresent = (sensorFlags & 0x01) != 0,
                MotionSensorPresent = (sensorFlags & 0x02) != 0,
                CaseOpen = (statusFlags & 0x01) != 0,
                MotionActive = (statusFlags & 0x02) != 0,
                MotionEvents = (CameraMotionEvents)eventMask
            };
        }
    }
}
