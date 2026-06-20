using CamPortal.Contracts.Dtos.SystemSettingsDtos;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface ISystemSettingsRepository
    {
        Task<SystemSettingsDto> GetSystemSettingsAsync();

        Task<bool> UpdateSystemSettingsAsync(SystemSettingsDto systemSettingsDto);
    }
}
