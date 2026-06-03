using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.CameraDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.ESPSessionTokenDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Infrastructure.Data;
using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class DeviceRepository : IDeviceRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public DeviceRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task<Guid> CreateDeviceAsync(CreateDeviceDto dto, IUnitOfWork? uow = null)
        {
            var deviceEntity = _mapper.Map<Device>(dto);

            if (deviceEntity.Id == Guid.Empty)
            {
                deviceEntity.Id = Guid.NewGuid();
            }

            if (uow != null)
            {
                var ownedDb = (CamPortalDBContext)uow.Db;
                ownedDb.Devices.Add(deviceEntity);

                return deviceEntity.Id;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            db.Devices.Add(deviceEntity);

            await db.SaveChangesAsync();

            return deviceEntity.Id;
        }

        public async Task<bool> DeleteDeviceAsync(Guid deviceId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.Devices
                .Where(x => x.Id == deviceId)
                .ExecuteDeleteAsync();

            return result != 0;
        }

        public async Task<DeviceDto?> GetDeviceByIdAsync(Guid deviceId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.Devices
                .AsNoTracking()
                .Where(x => x.Id == deviceId)
                .Select(device => _mapper.Map<DeviceDto>(device))
                .FirstOrDefaultAsync();

            return result;
        }

        public async Task<DevicePairStatus?> GetDeviceStatusAsync(Guid deviceId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.Devices
                .AsNoTracking()
                .Where(x => x.Id == deviceId)
                .Select(x => x.PairStatus)
                .FirstOrDefaultAsync();

            return result;
        }

        public async Task<bool> SetDeviceNameAsync(Guid deviceId, string name)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.Devices
                .Where(x => x.Id == deviceId)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.Name, name).SetProperty(c => c.UpdatedAt, DateTime.UtcNow));

            return result != 0;
        }

        public async Task<string?> GetDeviceNameAsync(Guid deviceId)
        {
            var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.Devices
                .AsNoTracking()
                .Where(x => x.Id == deviceId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> SetDeviceStatusAsync(Guid deviceId, DevicePairStatus newStatus, IUnitOfWork? uow = null)
        {
            if (uow != null)
            {
                var ownedDb = (CamPortalDBContext)uow.Db;
                var rowsAffected = await ownedDb.Devices
                    .Where(x => x.Id == deviceId)
                    .ExecuteUpdateAsync(x => x.SetProperty(c => c.PairStatus, newStatus).SetProperty(c => c.UpdatedAt, DateTime.UtcNow));

                return rowsAffected != 0;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.Devices
                .Where(x => x.Id == deviceId)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.PairStatus, newStatus).SetProperty(c => c.UpdatedAt, DateTime.UtcNow));

            return result != 0;
        }

        public async Task<bool> UpdateDeviceIpAsync(Guid deviceId, string newIpv4)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.Devices
                .Where(x => x.Id == deviceId)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.Ipv4Address, newIpv4).SetProperty(c => c.UpdatedAt, DateTime.UtcNow));

            return result != 0;
        }

        public async Task<List<DeviceDto>> GetAllDevicesAsync(params List<Guid> ids)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var query = db.Devices
                .AsNoTracking();

            if (ids != null && ids.Count > 0)
            {
                query = query.Where(x => ids != null && ids.Count != 0 && ids.Contains(x.Id));
            }

            var result = await query.Select(device => _mapper.Map<DeviceDto>(device))
                .ToListAsync();

            return result;
        }

        public async Task<List<DeviceDto>> GetAllDevicesWithStatusesAsync(params DevicePairStatus[] withStatuses)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.Devices
                .AsNoTracking()
                .Where(x => withStatuses.Contains(x.PairStatus))
                .Select(device => _mapper.Map<DeviceDto>(device))
                .ToListAsync();

            return result;
        }

        public async Task<bool> SetSessionTokenAsync(SetESPSessionTokenDto dto)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.Devices
                .Where(x => x.Ipv4Address == dto.Ipv4 && dto.AllowedStatuses.Contains(x.PairStatus))
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.SessionToken, dto.SessionToken)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                    .SetProperty(x => x.SessionTokenExpirationDate, dto.ExpirationDate));

            return result != 0;
        }

        public async Task<List<NameAndIdWithStatusDto>> GetAllDeviceNameAndIdAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.Devices
                .AsNoTracking()
                .Select(device => _mapper.Map<NameAndIdWithStatusDto>(device))
                .ToListAsync();

            return result;
        }

        public async Task<int> GetTotalDevicesAsync(params DevicePairStatus[] status)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var query = db.Devices
                .AsNoTracking();

            if (status != null && status.Length > 0)
            {
                query = query.Where(x => status != null && status.Length != 0 && status.Contains(x.PairStatus));
            }

            var result = await query.CountAsync();

            return result;
        }

        public async Task<bool> DoesDeviceExistAsync(Guid id)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var result = await db.Devices
                .AnyAsync(x => x.Id == id);
            return result;
        }

        public async Task<bool> UpdateDeviceAsync(UpdateDeviceDto dto, IUnitOfWork? uow = null)
        {
            if (uow != null)
            {
                var ownedDb = (CamPortalDBContext)uow.Db;
                var rowsAffected = await ownedDb.Devices
                    .Where(x => x.Id == dto.Id)
                    .ExecuteUpdateAsync(x => x
                        .SetProperty(c => c.PublicKey, dto.PublicKey)
                        .SetProperty(c => c.Fingerprint, dto.Fingerprint)
                        .SetProperty(c => c.PairStatus, dto.PairStatus)
                        .SetProperty(c => c.UpdatedAt, DateTime.UtcNow));

                return rowsAffected != 0;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.Devices
                .Where(x => x.Id == dto.Id)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(c => c.PublicKey, dto.PublicKey)
                    .SetProperty(c => c.Fingerprint, dto.Fingerprint)
                    .SetProperty(c => c.PairStatus, dto.PairStatus)
                    .SetProperty(c => c.UpdatedAt, DateTime.UtcNow));
            return result != 0;
        }

        public async Task<DeviceDto?> GetDeviceByIdWithStatusAsync(Guid deviceId, DevicePairStatus status)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var result = await db.Devices
                .AsNoTracking()
                .Include(x => x.PreprovisionAttempts)
                .Where(x => x.Id == deviceId && x.PairStatus == status)
                .Select(x => _mapper.Map<DeviceDto>(x))
                .FirstOrDefaultAsync();

            return result;
        }

        public async Task<DeviceStreamingHandshakeDto?> GetDeviceForStreamingHandshakeAsync(Guid deviceId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var result = await db.Devices
                .AsNoTracking()
                .Where(x => x.Id == deviceId)
                .Select(x => new DeviceStreamingHandshakeDto
                {
                    Id = x.Id,
                    PairStatus = x.PairStatus,
                    PublicKey = x.PublicKey,
                    DeviceVariant = x.DeviceType!.DeviceVariant,
                    DeviceName = x.Name,
                    CameraStreamingConfiguration = new()
                    {
                        Brightness = x.CameraConfiguration!.Brightness,
                        Contrast = x.CameraConfiguration.Contrast,
                        FlipMode = x.CameraConfiguration.FlipMode,
                        CameraAspectRatio = x.CameraConfiguration.CameraAspectRatio,
                        FrameRotation = x.CameraConfiguration.FrameRotation,
                        SharpenFactor = x.CameraConfiguration.SharpenFactor,
                        ZoomFactor = x.CameraConfiguration.ZoomFactor,
                        ZoomStartX = x.CameraConfiguration.ZoomStartX,
                        ZoomStartY = x.CameraConfiguration.ZoomStartY
                    }
                })
                .FirstOrDefaultAsync();

            return result;
        }
    }
}
