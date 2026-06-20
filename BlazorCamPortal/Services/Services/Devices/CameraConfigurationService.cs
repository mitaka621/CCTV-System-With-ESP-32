using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Models;
using CamPortal.Core.Utilities;

namespace CamPortal.Core.Services.Devices
{
    public class CameraConfigurationService : ICameraConfigurationService
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly ICameraConfigurationRepository _cameraConfigurationRepository;
        private readonly IMapper _mapper;
        private readonly IActiveCameraConnections _activeCameraConnectionsService;
        private readonly IUserCameraLayoutRepository _userCameraLayoutRepository;

        public CameraConfigurationService(ICameraConfigurationRepository cameraConfigurationRepository, IMapper mapper, IDeviceRepository deviceRepository, IActiveCameraConnections activeCameraConnectionsService, IUserCameraLayoutRepository userCameraLayoutRepository)
        {
            _cameraConfigurationRepository = cameraConfigurationRepository;
            _mapper = mapper;
            _deviceRepository = deviceRepository;
            _activeCameraConnectionsService = activeCameraConnectionsService;
            _userCameraLayoutRepository = userCameraLayoutRepository;
        }

        public async Task<bool> UpdateCameraConfigurationAsync(CameraConfigurationModel model)
        {
            if (!MiscUtilities.ValidateModel(model))
            {
                return false;
            }

            var cameraNameUpdate = await _deviceRepository.SetDeviceNameAsync(model.DeviceId, model.CameraName);

            var cameraConfigUpdate = await _cameraConfigurationRepository.UpdateDeviceConfigurationAsync(model.DeviceId, _mapper.Map<CameraStreamingConfigurationDto>(model));

            _activeCameraConnectionsService.TryDisconnect(model.DeviceId);

            var result = cameraNameUpdate && cameraConfigUpdate;

            if (result)
            {
                await _userCameraLayoutRepository.DeleteExisitingLayoutsForCameraAsync(model.DeviceId);
            }

            return result;
        }

        public async Task<CameraConfigurationModel?> GetCameraConfigurationAsync(Guid cameraId)
        {
            return _mapper.Map<CameraConfigurationModel>(await _cameraConfigurationRepository.GetCameraConfigurationAsync(cameraId));
        }

        public async Task<CameraResolutionDto?> GetCameraResolutionAsync(Guid cameraId)
        {
            return await _cameraConfigurationRepository.GetCameraResolutionAsync(cameraId);
        }

        public async Task<bool> SetCameraResolutionAsync(CameraResolutionDto dto)
        {
            var result = await _cameraConfigurationRepository.UpdateCameraResolutionAsync(dto);

            return result;
        }
    }
}
