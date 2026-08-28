using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.DeviceTypeDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Models;
using CamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories.PerDeviceRepository
{
    public class CameraRepository : DeviceRepository, ICameraRepository
    {
        public CameraRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper) : base(dbContextFactory, mapper) { }

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
                        FrameRotation = y.CameraConfiguration.FrameRotation,
                        SharpenFactor = y.CameraConfiguration.SharpenFactor,
                        ZoomFactor = y.CameraConfiguration.ZoomFactor,
                        ZoomStartX = y.CameraConfiguration.ZoomStartX,
                        ZoomStartY = y.CameraConfiguration.ZoomStartY,
                        ResolutionHeight = y.CameraConfiguration.ResolutionHeight,
                        ResolutionWidth = y.CameraConfiguration.ResolutionWidth
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
                   CameraConfiguration = Mapper.Map<CameraStreamingConfigurationDto>(x.CameraConfiguration)
               })
               .ToListAsync();

            return result;
        }
    }
}
