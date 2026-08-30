using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.SecurityDtos;
using CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Exceptions;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Channels;

namespace CamPortal.Core.Services.DeviceSessionHandlers
{
    public class SmokeAlarmSessionHandler : IDeviceSessionHandler, ISmokeAlarmCommandDispatcher
    {
        private const int _payloadLength = 15;
        private const int _maxSessionDurationSeconds = 60;

        private const double _chargeSenseThresholdToleranceVolts = 0.01;

        private readonly ILogger<SmokeAlarmSessionHandler> _logger;
        private readonly ISmokeAlarmDetectorManagerService _smokeAlarmDetectorManagerService;
        private readonly ISmokeAlarmDetectorConfigurationRepository _smokeAlarmDetectorConfigurationRepository;

        private readonly ConcurrentDictionary<Guid, Channel<OutboundDeviceMessageDto>> _commandChannels = new();

        public SmokeAlarmSessionHandler(ILogger<SmokeAlarmSessionHandler> logger, ISmokeAlarmDetectorManagerService smokeAlarmDetectorManagerService, ISmokeAlarmDetectorConfigurationRepository smokeAlarmDetectorConfigurationRepository)
        {
            _logger = logger;
            _smokeAlarmDetectorManagerService = smokeAlarmDetectorManagerService;
            _smokeAlarmDetectorConfigurationRepository = smokeAlarmDetectorConfigurationRepository;
        }

        public DeviceTypeCategories DeviceCategory => DeviceTypeCategories.SmokeAlarm;

        public async Task RunRecieveLoopAsync(ISecureChannel secureChannel, DeviceStreamingHandshakeDto device, CancellationToken cancellationToken)
        {
            using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idleCts.CancelAfter(TimeSpan.FromSeconds(_maxSessionDurationSeconds));

            byte[] plaintext;
            try
            {
                plaintext = await secureChannel.ReceiveAsync(idleCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                _logger.LogWarning("Smoke Alarm {DeviceId}: idle timeout", device.Id);
                return;
            }
            catch (SecureChannelProtocolException ex)
            {
                _logger.LogWarning("Smoke Alarm {DeviceId}: {Reason}", device.Id, ex.Message);
                return;
            }
            catch (CryptographicException ex)
            {
                _logger.LogWarning(ex, "Smoke Alarm {DeviceId}: frame decryption failed", device.Id);
                return;
            }
            catch (EndOfStreamException)
            {
                return;
            }

            if (plaintext.Length < _payloadLength)
            {
                _logger.LogWarning("Smoke Alarm {DeviceId}: frame too small ({Length} bytes)", device.Id, plaintext.Length);
                return;
            }

            var payloadByteArray = plaintext.AsSpan(0, _payloadLength);

            var payload = new SmokeAlarmDetectorPayloadDto
            {
                DeviceId = device.Id,
                LoggedTimeUTC = DateTime.UtcNow,
                PayloadVersion = payloadByteArray[0],
                Event = (SmokeAlarmEvent)payloadByteArray[1],
                BatterySOCPercent = payloadByteArray[2] == 0xFF ? double.NaN : payloadByteArray[2],
                BatteryVoltage = BinaryPrimitives.ReadUInt16BigEndian(payloadByteArray.Slice(3, 2)) / 1000.0,
                IsCharging = (payloadByteArray[5] & 0x01) != 0,
                BootCount = (int)BinaryPrimitives.ReadUInt32BigEndian(payloadByteArray.Slice(6, 4)),
                DetectedAlarmBeepCount = payloadByteArray[10],
                ChargingSenseVolts = BinaryPrimitives.ReadUInt16BigEndian(payloadByteArray.Slice(11, 2)) / 1000.0,
                ChargeSenseVoltageThreashold = BinaryPrimitives.ReadUInt16BigEndian(payloadByteArray.Slice(13, 2)) / 1000.0,
            };

            await _smokeAlarmDetectorManagerService.IngestAsync(payload);

            await EnqueueOutOfSyncConfigAsync(device.Id, payload);

            await SendQueuedCommandsAsync(secureChannel, device.Id, cancellationToken);

            if (!DeviceSessionHelper.TryBuildPayload(new OutboundDeviceMessageDto { Command = DeviceCommand.PayloadAck, }, out var responsePayloadBytes))
            {
                _logger.LogError("Could not build smoke alarm response payload");
            }

            await secureChannel.SendAsync(responsePayloadBytes, cancellationToken);
        }

        public Task RunSendLoopAsync(ISecureChannel secureChannel, DeviceStreamingHandshakeDto device, CancellationToken cancellationToken)
        {
            return Task.Delay(_maxSessionDurationSeconds * 1000);
        }

        public bool TryEnqueueConfig(Guid deviceId, params List<DeviceEspConfigDto> config)
        {
            return GetOrCreateCommandChannel(deviceId).Writer.TryWrite(new OutboundDeviceMessageDto { Command = DeviceCommand.SaveNewConfig, Config = config });
        }

        private async Task EnqueueOutOfSyncConfigAsync(Guid deviceId, SmokeAlarmDetectorPayloadDto payload)
        {
            var configuration = await _smokeAlarmDetectorConfigurationRepository.GetSmokeAlarmConfigurationAsync(deviceId);

            if (configuration is null)
            {
                return;
            }

            if (Math.Abs(configuration.ChargeSenseVoltageThreashold - payload.ChargeSenseVoltageThreashold) <= _chargeSenseThresholdToleranceVolts)
            {
                return;
            }

            _logger.LogInformation("Stream {DeviceId}: charge sense threshold out of sync, device is running {Reported} V but {Configured} V is configured, queueing a config push",
                deviceId, payload.ChargeSenseVoltageThreashold, configuration.ChargeSenseVoltageThreashold);

            TryEnqueueConfig(deviceId, new DeviceEspConfigDto
            {
                ConfigurationPropertyName = nameof(SmokeAlarmAvailibleConfigsToEdit.ChargeSense),
                Value = configuration.ChargeSenseVoltageThreashold,
            });
        }

        private Channel<OutboundDeviceMessageDto> GetOrCreateCommandChannel(Guid deviceId)
        {
            return _commandChannels.GetOrAdd(deviceId, _ => Channel.CreateBounded<OutboundDeviceMessageDto>(new BoundedChannelOptions(10)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false,
            }));
        }

        private async Task SendQueuedCommandsAsync(ISecureChannel secureChannel, Guid deviceId, CancellationToken cancellationToken)
        {
            var channel = GetOrCreateCommandChannel(deviceId);

            while (channel.Reader.TryRead(out var messageToSend))
            {
                if (!DeviceSessionHelper.TryBuildPayload(messageToSend, out var payload))
                {
                    continue;
                }

                try
                {
                    await secureChannel.SendAsync(payload, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    channel.Writer.TryWrite(messageToSend);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Stream {DeviceId}: failed to send outbound message", deviceId);
                    channel.Writer.TryWrite(messageToSend);
                    return;
                }

                _logger.LogInformation("Stream {DeviceId}: sent command {Command}", deviceId, messageToSend);
            }
        }
    }
}
