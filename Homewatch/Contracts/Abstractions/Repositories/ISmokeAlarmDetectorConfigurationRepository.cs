using Homewatch.Contracts.Abstractions.UnitOfWork;
using Homewatch.Contracts.Dtos.SmokeAlarmDetectorDtos;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface ISmokeAlarmDetectorConfigurationRepository
    {
        Task AddDefaultSmokeAlarmConfigurationToDeviceAsync(Guid deviceId, IUnitOfWork? uow = null);

        Task<bool> UpdateConfigurationAsync(Guid deviceId, SmokeAlarmConfigurationDto configuration);

        Task<SmokeAlarmConfigurationDto> GetSmokeAlarmConfigurationAsync(Guid deviceId);

        Task<int> CountDeviceConfigurationsAsync();

        Task<List<SmokeAlarmConfigurationDto>> GetAllConfigurationsAsync(params Guid[] deviceIds);
    }
}
