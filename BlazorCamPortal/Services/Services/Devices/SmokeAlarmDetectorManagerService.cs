using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos;
using Microsoft.Extensions.Logging;

namespace CamPortal.Core.Services.Devices
{
    public class SmokeAlarmDetectorManagerService : ISmokeAlarmDetectorManagerService
    {
        private readonly ISmokeAlarmTelemetryRepository _smokeAlarmDetectorRepository;
        private readonly ILogger<SmokeAlarmDetectorManagerService> _logger;

        public event Action<SmokeAlarmDetectorPayloadDto>? OnNewTelemetryAdded;

        public SmokeAlarmDetectorManagerService(ISmokeAlarmTelemetryRepository smokeAlarmDetectorRepository, ILogger<SmokeAlarmDetectorManagerService> logger)
        {
            _smokeAlarmDetectorRepository = smokeAlarmDetectorRepository;
            _logger = logger;
        }

        public async Task IngestAsync(SmokeAlarmDetectorPayloadDto payload)
        {
            if (!await _smokeAlarmDetectorRepository.SavePayloadAsync(payload))
            {
                _logger.LogError("Failed to save payload for device {DeviceId}: {Payload}", payload.DeviceId, payload);
                return;
            }

            var handlers = OnNewTelemetryAdded;
            if (handlers != null)
            {
                foreach (Action<SmokeAlarmDetectorPayloadDto> handler in handlers.GetInvocationList())
                {
                    var localHandler = handler;
                    _ = Task.Run(async () =>
                    {
                        try { localHandler(payload); }
                        catch (Exception ex) { _logger.LogError(ex, "Smoke alarm handler failed for device {DeviceId}", payload.DeviceId); }
                    });
                }
            }
        }

        public Task<SmokeAlarmDetectorPayloadDto?> GetLatestTelemetryAsync(Guid deviceId)
        {
            return _smokeAlarmDetectorRepository.GetLatestTelemetryForDetectorAsync(deviceId);
        }
    }
}
