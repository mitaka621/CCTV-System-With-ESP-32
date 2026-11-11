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
        private readonly ICameraSecurityCoordinator _cameraSecurityCoordinator;

        public CameraConfigurationService(ICameraConfigurationRepository cameraConfigurationRepository, IMapper mapper, IDeviceRepository deviceRepository, IActiveCameraConnections activeCameraConnectionsService, IUserCameraLayoutRepository userCameraLayoutRepository, ICameraSecurityCoordinator cameraSecurityCoordinator)
        {
            _cameraConfigurationRepository = cameraConfigurationRepository;
            _mapper = mapper;
            _deviceRepository = deviceRepository;
            _activeCameraConnectionsService = activeCameraConnectionsService;
            _userCameraLayoutRepository = userCameraLayoutRepository;
            _cameraSecurityCoordinator = cameraSecurityCoordinator;
        }

        public async Task<bool> UpdateCameraConfigurationAsync(CameraConfigurationModel model)
        {
            if (!MiscUtilities.ValidateModel(model))
            {
                return false;
            }

            var previousConfig = await _cameraConfigurationRepository.GetCameraConfigurationAsync(model.DeviceId);

            var cameraNameUpdate = await _deviceRepository.SetDeviceNameAsync(model.DeviceId, model.CameraName);

            var cameraConfigUpdate = await _cameraConfigurationRepository.UpdateDeviceConfigurationAsync(model.DeviceId, _mapper.Map<CameraStreamingConfigurationDto>(model));

            // a connected camera reconnects by itself after applying a new sensor resolution, so the server disconnect is only needed otherwise
            var isSensorResolutionSentToCamera = cameraConfigUpdate
                && previousConfig?.SensorResolution != model.SensorResolution
                && _cameraSecurityCoordinator.RequestSensorResolutionChange(model.DeviceId, model.SensorResolution);

            if (!isSensorResolutionSentToCamera)
            {
                _activeCameraConnectionsService.TryDisconnect(model.DeviceId);
            }

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

    }
}
