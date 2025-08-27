using AutoMapper;
using BlazorCamPortal.Contracts.Abstractions.Repositories;
using BlazorCamPortal.Contracts.Dtos.CameraDtos;
using BlazorCamPortal.Contracts.Dtos.ESPSessionTokenDtos;
using BlazorCamPortal.Contracts.Enums;
using BlazorCamPortal.Infrastructure.Data;
using BlazorCamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorCamPortal.Infrastructure.Repositories
{
    public class CameraRepository : ICameraRepository
    {
        private readonly CamPortalDBContext _dbContext;
        private readonly IMapper _mapper;

        public CameraRepository(CamPortalDBContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }

        public async Task<Guid> CreateCameraAsync(CreateCameraDto dto)
        {
            var cameraEntity = _mapper.Map<Camera>(dto);

            _dbContext.Cameras.Add(cameraEntity);

            await _dbContext.SaveChangesAsync();

            return cameraEntity.Id;
        }

        public async Task<bool> DeleteCameraAsync(Guid cameraId)
        {
            var result = await _dbContext.Cameras
                .Where(x => x.Id == cameraId)
                .ExecuteDeleteAsync();

            return result != 0;
        }

        public async Task<bool> DoesCameraIpExistAsync(string ipv4Address)
        {
            return await _dbContext.Cameras
                .AnyAsync(x => x.Ipv4Address == ipv4Address);
        }

        public async Task<bool> DoesCameraMacExistAsync(string macAddress)
        {
            return await _dbContext.Cameras
                .AnyAsync(x => x.MacAddress == macAddress);
        }

        public async Task<CameraDto?> GetCameraByIdAsync(Guid cameraId)
        {
            var result = await _dbContext.Cameras
                .AsNoTracking()
                .Where(x => x.Id == cameraId)
                .Select(camera => _mapper.Map<CameraDto>(camera))
                .FirstOrDefaultAsync();

            return result;
        }


        public async Task<CameraDto?> GetCameraByMacAsync(string mac)
        {
            var result = await _dbContext.Cameras
                .AsNoTracking()
                .Where(x => x.MacAddress == mac)
                .Select(camera => _mapper.Map<CameraDto>(camera))
                .FirstOrDefaultAsync();

            return result;
        }

        public async Task<PairStatus?> GetCameraStatusAsync(Guid cameraId)
        {
            var result = await _dbContext.Cameras
                .AsNoTracking()
                .Where(x => x.Id == cameraId)
                .Select(x => x.PairStatus)
                .FirstOrDefaultAsync();

            return result;
        }

        public async Task<bool> SetCameraNameAsync(Guid cameraId, string name)
        {
            var result = await _dbContext.Cameras
                .Where(x => x.Id == cameraId)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.Name, name).SetProperty(c => c.UpdatedAt, DateTime.Now));

            return result != 0;
        }

        public async Task<bool> SetCameraStatusAsync(Guid cameraId, PairStatus newStatus)
        {
            var result = await _dbContext.Cameras
                .Where(x => x.Id == cameraId)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.PairStatus, newStatus).SetProperty(c => c.UpdatedAt, DateTime.Now));

            return result != 0;
        }

        public async Task<bool> SetCameraStatusAsync(string mac, PairStatus newStatus)
        {
            var result = await _dbContext.Cameras
                .Where(x => x.MacAddress == mac)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.PairStatus, newStatus).SetProperty(c => c.UpdatedAt, DateTime.Now));

            return result != 0;
        }

        public async Task<bool> UpdateCameraIpAsync(Guid cameraId, string newIpv4)
        {
            var result = await _dbContext.Cameras
                .Where(x => x.Id == cameraId)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.Ipv4Address, newIpv4).SetProperty(c => c.UpdatedAt, DateTime.Now));

            return result != 0;
        }

        public async Task<CameraDto?> GetCameraByIpAsync(string ipv4)
        {
            var result = await _dbContext.Cameras
                .AsNoTracking()
                .Where(x => x.Ipv4Address == ipv4)
                .Select(camera => _mapper.Map<CameraDto>(camera))
                .FirstOrDefaultAsync();

            return result;
        }

        public async Task<bool> DoesCameraExistWithStatusAsync(string ipv4, string mac, PairStatus[] statuses)
        {
            var result = await _dbContext.Cameras
                .AnyAsync(x => x.Ipv4Address == ipv4 && x.MacAddress == mac && statuses.Contains(x.PairStatus));

            return result;
        }

        public async Task<bool> DoesCameraExistWithStatusAsync(string ipv4, PairStatus[] statuses)
        {
            var result = await _dbContext.Cameras
                .AnyAsync(x => x.Ipv4Address == ipv4 && statuses.Contains(x.PairStatus));

            return result;
        }

        public async Task<List<CameraDto>> GetAllCamerasAsync(params List<Guid> ids)
        {
            var query = _dbContext.Cameras
                .AsNoTracking();

            if (ids != null && ids.Count > 0)
            {
                query = query.Where(x => ids != null && ids.Count != 0 && ids.Contains(x.Id));
            }

            var result = await query.Select(camera => _mapper.Map<CameraDto>(camera))
                .ToListAsync();

            return result;
        }

        public async Task<List<CameraDto>> GetAllCamerasAsync(PairStatus[] statuses)
        {
            var result = await _dbContext.Cameras
                .AsNoTracking()
                .Where(x => statuses.Contains(x.PairStatus))
                .Select(camera => _mapper.Map<CameraDto>(camera))
                .ToListAsync();

            return result;
        }

        public async Task<bool> SetSessionTokenAsync(SetESPSessionTokenDto dto)
        {
            var result = await _dbContext.Cameras
                .Where(x => x.Ipv4Address == dto.Ipv4 && x.MacAddress == dto.Mac && dto.AllowedStatuses.Contains(x.PairStatus))
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.SessionToken, dto.SessionToken)
                    .SetProperty(x => x.UpdatedAt, DateTime.Now)
                    .SetProperty(x => x.SessionTokenExpirationDate, dto.ExpirationDate));

            return result != 0;
        }

        public async Task<ESPSessionTokenDto?> GetSessionTokenAsync(string ipv4, string mac)
        {
            var result = await _dbContext.Cameras
                .AsNoTracking()
                .Where(x => x.Ipv4Address == ipv4 && x.MacAddress == mac && x.PairStatus == PairStatus.Paired)
                .Select(x => new ESPSessionTokenDto()
                {
                    SessionToken = x.SessionToken,
                    SessionTokenExpirationDate = x.SessionTokenExpirationDate
                })
                .FirstOrDefaultAsync();

            return result;
        }

        public async Task<List<string>> GetAllPairedCamerasAsync()
        {
            var result = await _dbContext.Cameras
                .AsNoTracking()
                .Where(x => x.PairStatus == PairStatus.Paired)
                .Select(x => x.Ipv4Address)
                .ToListAsync();

            return result;
        }

        public async Task<Guid> GetCameraIdAsync(string ipv4, string mac)
        {
            var result = await _dbContext.Cameras
                .AsNoTracking()
                .Where(x => x.Ipv4Address == ipv4 && x.MacAddress == mac)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            return result;
        }

        public async Task<List<NameAndIdWithStatusDto>> GetAllCameraNameAndIdAsync()
        {
            var result = await _dbContext.Cameras
                .AsNoTracking()
                .Select(camera => _mapper.Map<NameAndIdWithStatusDto>(camera))
                .ToListAsync();

            return result;
        }
    }
}
