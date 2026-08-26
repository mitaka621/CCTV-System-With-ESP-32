using CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ISmokeAlarmDetectorManagerService
    {
        public event Action<SmokeAlarmDetectorPayloadDto>? OnNewTelemetryAdded;

        Task IngestAsync(SmokeAlarmDetectorPayloadDto payload);

        Task<SmokeAlarmDetectorPayloadDto?> GetLatestTelemetryAsync(Guid deviceId);
    }
}
