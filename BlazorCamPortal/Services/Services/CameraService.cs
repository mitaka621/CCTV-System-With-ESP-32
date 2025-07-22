using AutoMapper;
using BlazorCamPortal.Contracts.Abstractions.Repositories;
using BlazorCamPortal.Contracts.Abstractions.Services;
using BlazorCamPortal.Contracts.Dtos;
using BlazorCamPortal.Contracts.Enums;
using BlazorCamPortal.Contracts.Models;

namespace BlazorCamPortal.Core.Services
{
    public class CameraService : ICameraService
    {
        private readonly ICameraRepository _cameraRepository;
        private readonly IMapper _mapper;
        private readonly IDeviceAuthenticatorService _deviceAuthenticatorService;

        public CameraService(ICameraRepository cameraRepository, IMapper mapper, IDeviceAuthenticatorService deviceAuthenticatorService)
        {
            _cameraRepository = cameraRepository;
            _mapper = mapper;
            _deviceAuthenticatorService = deviceAuthenticatorService;
        }

        public async Task<Guid> CreateCameraAsync(CreateCameraModel model, PairStatus cameraStatus)
        {
            var dto = _mapper.Map<CreateCameraDto>(model);

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

        public async Task<bool> DoesCameraExistWithStatusAsync(string ipv4, string mac, PairStatus status)
        {
            return await _cameraRepository.DoesCameraExistWithStatusAsync(ipv4, mac, status);
        }

        public async Task<string?> GenerateSessionTokenForDeviceAsync(string ipv4, string mac)
        {
            var newSessionToken = _deviceAuthenticatorService.GenerateSessionToken();

            var result = await _cameraRepository.SetSessionTokenAsync(ipv4, mac, newSessionToken);

            if (result)
            {
                return newSessionToken;
            }

            return null;
        }

        public async Task<List<CameraDisplayModel>> GetAllCamerasAsync()
        {
            var result = await _cameraRepository.GetAllCamerasAsync();

            return _mapper.Map<List<CameraDisplayModel>>(result);
        }

        public async Task<bool> UpdateCameraStatusAsync(string mac, PairStatus newStatus)
        {
            return await _cameraRepository.SetCameraStatusAsync(mac, newStatus);
        }
    }
}
