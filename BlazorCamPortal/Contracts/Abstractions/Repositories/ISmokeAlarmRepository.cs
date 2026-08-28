using CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos;
using CamPortal.Contracts.Models;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface ISmokeAlarmRepository
    {
        Task<List<SmokeAlarmDto>> GetAllSmokeAlarmsWithTypeAndLatestTelemetryAsync(DeviceFilterModel filterModel);
    }
}
