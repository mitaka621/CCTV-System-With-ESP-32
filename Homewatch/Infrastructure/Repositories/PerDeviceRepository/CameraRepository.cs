using MapsterMapper;
using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Dtos.CameraConfigurationDtos;
using Homewatch.Contracts.Dtos.DeviceDtos;
using Homewatch.Contracts.Dtos.DeviceTypeDtos;
using Homewatch.Contracts.Enums;
using Homewatch.Contracts.Models;
using Homewatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Homewatch.Infrastructure.Repositories.PerDeviceRepository
{
    public class CameraRepository : DeviceRepository, ICameraRepository
    {
        public CameraRepository(IDbContextFactory<HomewatchDBContext> dbContextFactory, IMapper mapper) : base(dbContextFactory, mapper) { }

        public async Task<Dictionary<Guid, CameraInfoWithConfigurationDto>> GetAllCamerasWithConfigurationAsync()
        {
            var db = await DbContextFactory.CreateDbContextAsync();

            return await db.Devices
                .AsNoTracking()
                .Include(x => x.CameraConfiguration)
                .Where(x => x.DeviceType.DeviceCategory == DeviceTypeCategories.Camera
                    && x.PairStatus != DevicePairStatus.Removed)
                .OrderBy(x => x.CreatedAt)
                .ToDictionaryAsync(x => x.Id, y => new CameraInfoWithConfigurationDto()
                {
                    Id = y.Id,
                    Name = y.Name,
                    PairStatus = y.PairStatus,
                    CreatedAt = y.CreatedAt,
                    Fingerprint = y.Fingerprint,
                    Ipv4Address = y.Ipv4Address,
                    PublicKey = y.PublicKey,
                    UpdatedAt = y.UpdatedAt,
                    Configuration = new()
                    {
                        Brightness = y.CameraConfiguration!.Brightness,
                        Contrast = y.CameraConfiguration.Contrast,
                        FlipMode = y.CameraConfiguration.FlipMode,
                        CameraAspectRatio = y.CameraConfiguration.CameraAspectRatio,
                        SensorResolution = y.CameraConfiguration.SensorResolution,
                        FrameRotation = y.CameraConfiguration.FrameRotation,
                        ZoomFactor = y.CameraConfiguration.ZoomFactor,
                        ZoomStartX = y.CameraConfiguration.ZoomStartX,
                        ZoomStartY = y.CameraConfiguration.ZoomStartY
                    }
                });
        }

        public async Task<List<CameraDto>> GetAllCamerasWithTypeAndConfigurationAsync(DeviceFilterModel filterModel)
        {
            var db = await DbContextFactory.CreateDbContextAsync();

            var query = GetDeviceBaseFilterQuery(db, filterModel)
                .Where(x => x.DeviceType.DeviceCategory == DeviceTypeCategories.Camera);

            var result = await query
               .Select(x => new CameraDto()
               {
                   CreatedAt = x.CreatedAt,
                   Fingerprint = x.Fingerprint,
                   Id = x.Id,
                   Ipv4Address = x.Ipv4Address,
                   Name = x.Name,
                   PairStatus = x.PairStatus,
                   PublicKey = x.PublicKey,
                   UpdatedAt = x.UpdatedAt,
                   DeviceType = Mapper.Map<DeviceTypeDto>(x.DeviceType),
                   CameraConfiguration = Mapper.Map<CameraStreamingConfigurationDto>(x.CameraConfiguration!)
               })
               .ToListAsync();

            return result;
        }
    }
}
