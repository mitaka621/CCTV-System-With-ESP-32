using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class CameraConfigurationRepository : ICameraConfigurationRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;

        public CameraConfigurationRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task AddDefaultCameraConfigurationToDeviceAsync(Guid deviceId, IUnitOfWork? uow = null)
        {
            if (uow != null)
            {
                var ownedDb = (CamPortalDBContext)uow.Db;

                ownedDb.CameraConfigurations.Add(new() { DeviceId = deviceId });

                return;
            }

            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            dbContext.CameraConfigurations.Add(new() { DeviceId = deviceId });
            await dbContext.SaveChangesAsync();
        }

        public async Task<bool> UpdateDeviceConfigurationAsync(Guid deviceId, CameraStreamingConfigurationDto dto)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var numberOfUpdates = await dbContext.CameraConfigurations
                .Where(x => x.DeviceId == deviceId)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Contrast, dto.Contrast)
                    .SetProperty(y => y.Brightness, dto.Brightness)
                    .SetProperty(y => y.SharpenFactor, dto.SharpenFactor)
                    .SetProperty(y => y.FlipMode, dto.FlipMode)
                    .SetProperty(y => y.ZoomFactor, dto.ZoomFactor)
                    .SetProperty(y => y.ZoomStartX, dto.ZoomStartX)
                    .SetProperty(y => y.ZoomStartY, dto.ZoomStartY)
                    .SetProperty(y => y.FrameRotation, dto.FrameRotation));

            return numberOfUpdates >= 1;
        }
    }
}
