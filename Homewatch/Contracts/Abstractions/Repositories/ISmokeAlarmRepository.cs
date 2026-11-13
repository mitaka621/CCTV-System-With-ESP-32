using Homewatch.Contracts.Dtos.SmokeAlarmDetectorDtos;
using Homewatch.Contracts.Models;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface ISmokeAlarmRepository
    {
        Task<List<SmokeAlarmDto>> GetAllSmokeAlarmsWithTypeAndLatestTelemetryAsync(DeviceFilterModel filterModel);
    }
}
