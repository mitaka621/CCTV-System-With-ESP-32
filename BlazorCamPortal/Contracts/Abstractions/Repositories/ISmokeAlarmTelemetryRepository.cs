using CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface ISmokeAlarmTelemetryRepository
    {
        Task<bool> SavePayloadAsync(SmokeAlarmDetectorPayloadDto smokeAlarmDetectorPayload);

        Task<SmokeAlarmDetectorPayloadDto?> GetLatestTelemetryForDetectorAsync(Guid deviceId);
    }
}
