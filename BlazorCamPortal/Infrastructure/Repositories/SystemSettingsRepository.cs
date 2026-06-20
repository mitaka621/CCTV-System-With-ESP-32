using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Dtos.SystemSettingsDtos;
using CamPortal.Infrastructure.Data;
using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class SystemSettingsRepository : ISystemSettingsRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;

        public SystemSettingsRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<SystemSettingsDto> GetSystemSettingsAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var settings = await db.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == CamPortalDBContext.SystemSettingsId);

            if (settings == null)
            {
                return new SystemSettingsDto();
            }

            return new SystemSettingsDto
            {
                EncodedVideoRetention = settings.EncodedVideoRetention,
                CameraChunkRetention = settings.CameraChunkRetention
            };
        }

        public async Task<bool> UpdateSystemSettingsAsync(SystemSettingsDto systemSettingsDto)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var updated = await db.SystemSettings
                .Where(x => x.Id == CamPortalDBContext.SystemSettingsId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.EncodedVideoRetention, systemSettingsDto.EncodedVideoRetention)
                    .SetProperty(x => x.CameraChunkRetention, systemSettingsDto.CameraChunkRetention));

            if (updated > 0)
            {
                return true;
            }

            db.SystemSettings.Add(new SystemSettings
            {
                Id = CamPortalDBContext.SystemSettingsId,
                EncodedVideoRetention = systemSettingsDto.EncodedVideoRetention,
                CameraChunkRetention = systemSettingsDto.CameraChunkRetention
            });

            return await db.SaveChangesAsync() > 0;
        }
    }
}
