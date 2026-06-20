using CamPortal.Contracts.Dtos.SystemSettingsDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ISystemSettingsService
    {
        Task<SystemSettingsDto> GetSystemSettingsAsync();

        Task<bool> UpdateSystemSettingsAsync(SystemSettingsDto systemSettingsDto);
    }
}
