using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Dtos.SystemSettingsDtos;
using Homewatch.Infrastructure.Data;
using Homewatch.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Homewatch.Infrastructure.Repositories
{
    public class SystemSettingsRepository : ISystemSettingsRepository
    {
        private readonly IDbContextFactory<HomewatchDBContext> _dbContextFactory;

        public SystemSettingsRepository(IDbContextFactory<HomewatchDBContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<SystemSettingsDto> GetSystemSettingsAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var settings = await db.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == HomewatchDBContext.SystemSettingsId);

            if (settings == null)
            {
                return new SystemSettingsDto();
            }

            return new SystemSettingsDto
            {
                EncodedVideoRetention = settings.EncodedVideoRetention,
                CameraChunkRetention = settings.CameraChunkRetention,
                SecurityMinFps = settings.SecurityMinFps,
                SecurityMaxTemperatureC = settings.SecurityMaxTemperatureC,
                SecurityMaxHumidityPercent = settings.SecurityMaxHumidityPercent,
                DismissedNotificationRetention = settings.DismissedNotificationRetention
            };
        }

        public async Task<bool> UpdateSystemSettingsAsync(SystemSettingsDto systemSettingsDto)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var updated = await db.SystemSettings
                .Where(x => x.Id == HomewatchDBContext.SystemSettingsId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.EncodedVideoRetention, systemSettingsDto.EncodedVideoRetention)
                    .SetProperty(x => x.CameraChunkRetention, systemSettingsDto.CameraChunkRetention)
                    .SetProperty(x => x.SecurityMinFps, systemSettingsDto.SecurityMinFps)
                    .SetProperty(x => x.SecurityMaxTemperatureC, systemSettingsDto.SecurityMaxTemperatureC)
                    .SetProperty(x => x.SecurityMaxHumidityPercent, systemSettingsDto.SecurityMaxHumidityPercent)
                    .SetProperty(x => x.DismissedNotificationRetention, systemSettingsDto.DismissedNotificationRetention));

            if (updated > 0)
            {
                return true;
            }

            db.SystemSettings.Add(new SystemSettings
            {
                Id = HomewatchDBContext.SystemSettingsId,
                EncodedVideoRetention = systemSettingsDto.EncodedVideoRetention,
                CameraChunkRetention = systemSettingsDto.CameraChunkRetention,
                SecurityMinFps = systemSettingsDto.SecurityMinFps,
                SecurityMaxTemperatureC = systemSettingsDto.SecurityMaxTemperatureC,
                SecurityMaxHumidityPercent = systemSettingsDto.SecurityMaxHumidityPercent,
                DismissedNotificationRetention = systemSettingsDto.DismissedNotificationRetention
            });

            return await db.SaveChangesAsync() > 0;
        }
    }
}
