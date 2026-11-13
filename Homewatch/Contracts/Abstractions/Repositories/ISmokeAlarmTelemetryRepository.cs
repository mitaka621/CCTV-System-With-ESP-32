using Homewatch.Contracts.Dtos.SmokeAlarmDetectorDtos;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface ISmokeAlarmTelemetryRepository
    {
        Task<bool> SavePayloadAsync(SmokeAlarmDetectorPayloadDto smokeAlarmDetectorPayload);

        Task<SmokeAlarmDetectorPayloadDto?> GetLatestTelemetryForDetectorAsync(Guid deviceId);
    }
}
