using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.CameraDtos;
using CamPortal.Contracts.Dtos.ESPSessionTokenDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Models;
using Microsoft.Extensions.Configuration;

namespace CamPortal.Core.Services
{
    public class CameraService : ICameraService
    {
        private readonly ICameraRepository _cameraRepository;
        private readonly IMapper _mapper;
        private readonly IDeviceAuthenticatorService _deviceAuthenticatorService;
        private readonly ICameraFramesManagerService _cameraFramesManagerService;

        private readonly int _sessionTokenDurationInMinutes;

        public CameraService(
            ICameraRepository cameraRepository,
            IMapper mapper,
            IDeviceAuthenticatorService deviceAuthenticatorService,
            IConfiguration configuration,
            ICameraFramesManagerService cameraFramesManagerService)
        {
            _cameraRepository = cameraRepository;
            _mapper = mapper;
            _deviceAuthenticatorService = deviceAuthenticatorService;
            _cameraFramesManagerService = cameraFramesManagerService;

            _sessionTokenDurationInMinutes = int.Parse(configuration
                .GetSection("ESPCamera")["SessionTokenDurationInMinutes"]
                    ?? throw new ArgumentNullException("SessionTokenDurationInMinutes not set in config"));
        }

        public async Task<Guid> CreateCameraAsync(CreateCameraModel model, PairStatus cameraStatus)
        {
            var dto = _mapper.Map<CreateCameraDto>(model);

            if (string.IsNullOrEmpty(dto.Ipv4Address) || string.IsNullOrEmpty(dto.MacAddress))
            {
                throw new ArgumentException("Invalid ip or mac address");
            }

            dto.CreatedAt = DateTime.Now;
            dto.PairStatus = cameraStatus;

            var doesIpExist = await _cameraRepository.DoesCameraIpExistAsync(dto.Ipv4Address);
            var doesMacExist = await _cameraRepository.DoesCameraMacExistAsync(dto.MacAddress);

            if (!doesIpExist && !doesMacExist)
            {
                return await _cameraRepository.CreateCameraAsync(dto);
            }

            if (doesIpExist)
            {
                var existingCamera = await _cameraRepository.GetCameraByIpAsync(dto.Ipv4Address);

                if (existingCamera!.MacAddress != dto.MacAddress)
                {
                    throw new InvalidOperationException($"A camera with this IP {existingCamera.Ipv4Address} address already exists with a different MAC address.");
                }

                await _cameraRepository.SetCameraStatusAsync(existingCamera.Id, cameraStatus);

                return existingCamera.Id;
            }

            var existingCameraByMac = await _cameraRepository.GetCameraByMacAsync(dto.MacAddress);

            if (existingCameraByMac!.Ipv4Address != dto.Ipv4Address)
            {
                await _cameraRepository.UpdateCameraIpAsync(existingCameraByMac.Id, dto.Ipv4Address);
            }

            await _cameraRepository.SetCameraStatusAsync(existingCameraByMac.Id, cameraStatus);

            return existingCameraByMac.Id;
        }

        public async Task<bool> DoesCameraExistWithStatusAsync(string ipv4, string mac, PairStatus[] statuses)
        {
            return await _cameraRepository.DoesCameraExistWithStatusAsync(ipv4, mac, statuses);
        }

        public async Task<bool> DoesCameraExistWithStatusAsync(string ipv4, PairStatus[] statuses)
        {
            return await _cameraRepository.DoesCameraExistWithStatusAsync(ipv4, statuses);
        }

        public async Task<string?> GenerateSessionTokenForDeviceAsync(string ipv4, string mac)
        {
            var newSessionToken = _deviceAuthenticatorService.GenerateSessionToken();

            var result = await _cameraRepository.SetSessionTokenAsync(new SetESPSessionTokenDto()
            {
                Ipv4 = ipv4,
                Mac = mac,
                SessionToken = newSessionToken,
                ExpirationDate = DateTime.Now.AddMinutes(_sessionTokenDurationInMinutes),
                AllowedStatuses = [PairStatus.ServerChallengeSolved, PairStatus.SessionTokenExpired]
            });

            if (result)
            {
                return newSessionToken;
            }

            return null;
        }

        public async Task<(byte[]? token, bool isExpired)> GetSessionTokenAsByteArrayAsync(string ipv4, string mac)
        {
            var dto = await _cameraRepository.GetSessionTokenAsync(ipv4, mac);

            bool isSessionTokenExpired = dto != null && dto.SessionTokenExpirationDate < DateTime.Now;

            if (dto == null || dto.SessionToken == null || dto.SessionTokenExpirationDate == null)
            {
                return (null, false);
            }

            return (Convert.FromBase64String(dto.SessionToken), isSessionTokenExpired);
        }

        public async Task<List<CameraDisplayModel>> GetAllCamerasAsync()
        {
            var result = await _cameraRepository.GetAllCamerasAsync();

            return _mapper.Map<List<CameraDisplayModel>>(result);
        }

        public async Task<List<CameraDisplayModel>> GetAllCamerasAsync(params List<Guid> cameraIds)
        {
            var result = await _cameraRepository.GetAllCamerasAsync(cameraIds);

            return _mapper.Map<List<CameraDisplayModel>>(result);
        }

        public async Task<List<CameraDisplayModel>> GetAllCamerasAsync(params PairStatus[] statuses)
        {
            var result = await _cameraRepository.GetAllCamerasAsync(statuses);

            return _mapper.Map<List<CameraDisplayModel>>(result);
        }

        public async Task<bool> UpdateCameraStatusAsync(string mac, PairStatus newStatus)
        {
            return await _cameraRepository.SetCameraStatusAsync(mac, newStatus);
        }

        public async Task<List<string>> GetAllActiveCameraIpsAsync()
        {
            var cameras = await _cameraRepository.GetAllPairedCamerasAsync();

            return cameras;
        }

        public async Task ChangeCameraStatusAsync(Guid cameraId, PairStatus newStatus)
        {
            var result = await _cameraRepository.SetCameraStatusAsync(cameraId, newStatus);

            if (!result)
            {
                throw new InvalidOperationException($"Failed to change status for camera with ID {cameraId}");
            }
            else if (newStatus == PairStatus.Forgotten)
            {
                _cameraFramesManagerService.CloseChannel(cameraId);
            }
        }

        public async Task<Guid> GetCameraIdAsync(string ipv4, string mac)
        {
            var result = await _cameraRepository.GetCameraIdAsync(ipv4, mac);

            return result;
        }

        public async Task<List<NameAndIdWithStatusModel>> GetAllCameraNameAndIdAsync()
        {
            var result = await _cameraRepository.GetAllCameraNameAndIdAsync();

            return _mapper.Map<List<NameAndIdWithStatusModel>>(result);
        }

        public async Task<List<CreateCameraModel>> GetCamerasByIdAsync(List<Guid> cameraIds)
        {
            var allCameras = await _cameraRepository.GetAllCamerasAsync();
            var filteredCameras = allCameras.Where(c => cameraIds.Contains(c.Id)).ToList();
            return _mapper.Map<List<CreateCameraModel>>(filteredCameras);
        }

        public async Task<int> GetTotalCamerasAsync(params List<PairStatus> status)
        {
            var result = await _cameraRepository.GetTotalCamerasAsync(status);
            return result;
        }
    }
}