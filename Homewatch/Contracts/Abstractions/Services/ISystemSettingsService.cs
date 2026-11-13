using Homewatch.Contracts.Dtos.SystemSettingsDtos;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface ISystemSettingsService
    {
        Task<SystemSettingsDto> GetSystemSettingsAsync();

        Task<bool> UpdateSystemSettingsAsync(SystemSettingsDto systemSettingsDto);
    }
}
