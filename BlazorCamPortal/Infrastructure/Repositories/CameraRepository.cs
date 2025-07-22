using AutoMapper;
using BlazorCamPortal.Contracts.Abstractions.Repositories;
using BlazorCamPortal.Contracts.Dtos;
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

        public async Task<bool> DoesCameraExistWithStatusAsync(string ipv4, string mac, PairStatus status)
        {
            var result = await _dbContext.Cameras
                .AnyAsync(x => x.Ipv4Address == ipv4 && x.MacAddress == mac && x.PairStatus == status);

            return result;
        }

        public async Task<List<CameraDto>> GetAllCamerasAsync()
        {
            var result = await _dbContext.Cameras
                .AsNoTracking()
                .Select(camera => _mapper.Map<CameraDto>(camera))
                .ToListAsync();

            return result;
        }

        public async Task<bool> SetSessionTokenAsync(string ipv4, string mac, string sessionToken)
        {
            var result = await _dbContext.Cameras
                .Where(x => x.Ipv4Address == ipv4 && x.MacAddress == mac && x.PairStatus == PairStatus.ServerChallengeSolved)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.SessionToken, sessionToken).SetProperty(c => c.UpdatedAt, DateTime.Now));

            return result != 0;
        }
    }
}
