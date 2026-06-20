using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.SystemSettingsDtos;

namespace CamPortal.Core.Services.SystemSettings
{
    public class SystemSettingsService : ISystemSettingsService
    {
        private readonly ISystemSettingsRepository _systemSettingsRepository;

        public SystemSettingsService(ISystemSettingsRepository systemSettingsRepository)
        {
            _systemSettingsRepository = systemSettingsRepository;
        }

        public async Task<SystemSettingsDto> GetSystemSettingsAsync()
        {
            return await _systemSettingsRepository.GetSystemSettingsAsync();
        }

        public async Task<bool> UpdateSystemSettingsAsync(SystemSettingsDto systemSettingsDto)
        {
            return await _systemSettingsRepository.UpdateSystemSettingsAsync(systemSettingsDto);
        }
    }
}
