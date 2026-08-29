using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Exceptions;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CamPortal.Core.Services.DeviceSessionHandlers
{
    public class SmokeAlarmSessionHandler : IDeviceSessionHandler
    {
        private const int _payloadLength = 14;
        private const int _maxSessionDurationSeconds = 60;

        private readonly ILogger<SmokeAlarmSessionHandler> _logger;
        private readonly ISmokeAlarmDetectorManagerService _smokeAlarmDetectorManagerService;

        public SmokeAlarmSessionHandler(ILogger<SmokeAlarmSessionHandler> logger, ISmokeAlarmDetectorManagerService smokeAlarmDetectorManagerService)
        {
            _logger = logger;
            _smokeAlarmDetectorManagerService = smokeAlarmDetectorManagerService;
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
            };

            await _smokeAlarmDetectorManagerService.IngestAsync(payload);

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
    }
}
