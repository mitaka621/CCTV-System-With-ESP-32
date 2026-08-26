using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface ISmokeAlarmDetectorConfigurationRepository
    {
        Task AddDefaultSmokeAlarmConfigurationToDeviceAsync(Guid deviceId, IUnitOfWork? uow = null);

        Task<bool> UpdateConfigurationAsync(Guid deviceId, SmokeAlarmConfigurationDto configuration);

        Task<SmokeAlarmConfigurationDto> GetSmokeAlarmConfigurationAsync(Guid deviceId);

        Task<int> CountDeviceConfigurationsAsync();
    }
}
