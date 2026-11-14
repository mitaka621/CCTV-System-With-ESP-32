using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Dtos.SystemSettingsDtos;

namespace Homewatch.Core.Services.SystemServices
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
