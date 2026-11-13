using Homewatch.Contracts.Dtos.SystemSettingsDtos;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface ISystemSettingsRepository
    {
        Task<SystemSettingsDto> GetSystemSettingsAsync();

        Task<bool> UpdateSystemSettingsAsync(SystemSettingsDto systemSettingsDto);
    }
}
