using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class CameraConfigurationRepository : ICameraConfigurationRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public CameraConfigurationRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task<Dictionary<Guid, CameraAspectRatios>> GetCameraAspectRatiosAsync(IReadOnlyList<Guid> cameraIds)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await dbContext.CameraConfigurations
                .AsNoTracking()
                .Where(x => cameraIds.Contains(x.DeviceId))
                .ToDictionaryAsync(x => x.DeviceId, x => x.CameraAspectRatio);
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
                    .SetProperty(y => y.FrameRotation, dto.FrameRotation)
                    .SetProperty(y => y.CameraAspectRatio, dto.CameraAspectRatio));

            return numberOfUpdates >= 1;
        }

        public async Task<bool> UpdateCameraResolutionAsync(CameraResolutionDto dto)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var numberOfUpdates = await dbContext.CameraConfigurations
                .Where(x => x.DeviceId == dto.CameraId)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.ResolutionWidth, dto.Width)
                    .SetProperty(y => y.ResolutionHeight, dto.Height));

            return numberOfUpdates >= 1;
        }

        public async Task<CameraStreamingConfigurationDto?> GetCameraConfigurationAsync(Guid deviceId)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await dbContext.CameraConfigurations.AsNoTracking()
                .Where(x => x.DeviceId == deviceId)
                .Select(x => _mapper.Map<CameraStreamingConfigurationDto>(x))
                .FirstOrDefaultAsync();
        }

        public async Task<CameraResolutionDto?> GetCameraResolutionAsync(Guid deviceId)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await dbContext.CameraConfigurations.AsNoTracking()
                .Where(x => x.DeviceId == deviceId)
                .Select(x => new CameraResolutionDto
                {
                    Width = x.ResolutionWidth,
                    Height = x.ResolutionHeight
                })
                .FirstOrDefaultAsync();
        }
    }
}
