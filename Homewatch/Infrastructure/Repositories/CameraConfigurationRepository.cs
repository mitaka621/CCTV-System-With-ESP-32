using MapsterMapper;
using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Abstractions.UnitOfWork;
using Homewatch.Contracts.Dtos.CameraConfigurationDtos;
using Homewatch.Contracts.Dtos.SecurityDtos;
using Homewatch.Contracts.Enums;
using Homewatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Homewatch.Infrastructure.Repositories
{
    public class CameraConfigurationRepository : ICameraConfigurationRepository
    {
        private readonly IDbContextFactory<HomewatchDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public CameraConfigurationRepository(IDbContextFactory<HomewatchDBContext> dbContextFactory, IMapper mapper)
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
                var ownedDb = (HomewatchDBContext)uow.Db;

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
                    .SetProperty(y => y.FlipMode, dto.FlipMode)
                    .SetProperty(y => y.ZoomFactor, dto.ZoomFactor)
                    .SetProperty(y => y.ZoomStartX, dto.ZoomStartX)
                    .SetProperty(y => y.ZoomStartY, dto.ZoomStartY)
                    .SetProperty(y => y.FrameRotation, dto.FrameRotation)
                    .SetProperty(y => y.CameraAspectRatio, dto.CameraAspectRatio)
                    .SetProperty(y => y.SensorResolution, dto.SensorResolution));

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

        public async Task<bool> SetSecurityArmedAsync(Guid deviceId, bool armed)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.CameraConfigurations
                .Where(x => x.DeviceId == deviceId)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.SecurityArmed, armed));

            return result != 0;
        }

        public async Task<bool> SetCaseSensorInstalledAsync(Guid deviceId, bool installed)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.CameraConfigurations
                .Where(x => x.DeviceId == deviceId)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.CaseSensorInstalled, installed));

            return result != 0;
        }

        public async Task<bool> SetMovementThresholdsAsync(Guid deviceId, double movementThresholdOffset, double rotationThresholdOffset)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.CameraConfigurations
                .Where(x => x.DeviceId == deviceId)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(c => c.MovementThresholdOffset, movementThresholdOffset)
                    .SetProperty(c => c.RotationThresholdOffset, rotationThresholdOffset));

            return result != 0;
        }

        public async Task<bool> SetSensorResolutionAsync(Guid deviceId, CameraSensorResolutions sensorResolution)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.CameraConfigurations
                .Where(x => x.DeviceId == deviceId)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.SensorResolution, sensorResolution));

            return result != 0;
        }

        public async Task<DeviceSecurityConfigDto?> GetDeviceSecurityConfigAsync(Guid deviceId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.CameraConfigurations
                .AsNoTracking()
                .Where(x => x.DeviceId == deviceId)
                .Select(x => new DeviceSecurityConfigDto
                {
                    SecurityArmed = x.SecurityArmed,
                    CaseSensorInstalled = x.CaseSensorInstalled,
                    MovementThresholdOffset = x.MovementThresholdOffset,
                    RotationThresholdOffset = x.RotationThresholdOffset,
                    SensorResolution = x.SensorResolution
                })
                .FirstOrDefaultAsync();

            return result;
        }
    }
}
