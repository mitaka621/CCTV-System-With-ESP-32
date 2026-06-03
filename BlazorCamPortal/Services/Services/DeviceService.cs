using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.CameraDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.ESPSessionTokenDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Models;
using Microsoft.Extensions.Configuration;

namespace CamPortal.Core.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly IMapper _mapper;
        private readonly IDeviceAuthenticatorService _deviceAuthenticatorService;
        private readonly ICameraFramesManagerService _cameraFramesManagerService;
        private readonly IActiveCameraConnections _activeCameraConnections;
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        private readonly ICameraConfigurationRepository _cameraConfigurationRepository;

        private readonly int _sessionTokenDurationInMinutes;

        public DeviceService(
            IDeviceRepository cameraRepository,
            IMapper mapper,
            IDeviceAuthenticatorService deviceAuthenticatorService,
            IConfiguration configuration,
            ICameraFramesManagerService cameraFramesManagerService,
            IActiveCameraConnections activeCameraConnections,
            IDeviceTypeRepository deviceTypeRepository,
            ICameraConfigurationRepository cameraConfigurationRepository)
        {
            _deviceRepository = cameraRepository;
            _mapper = mapper;
            _deviceAuthenticatorService = deviceAuthenticatorService;
            _cameraFramesManagerService = cameraFramesManagerService;
            _activeCameraConnections = activeCameraConnections;
            _deviceTypeRepository = deviceTypeRepository;
            _cameraConfigurationRepository = cameraConfigurationRepository;

            _sessionTokenDurationInMinutes = int.Parse(configuration
                .GetSection("ESPCamera")["SessionTokenDurationInMinutes"]
                    ?? throw new ArgumentNullException("SessionTokenDurationInMinutes not set in config"));
        }


        public async Task<Guid> CreateDeviceAsync(CreateDeviceDto dto, IUnitOfWork? uow = null)
        {
            var deviceCategory = await _deviceTypeRepository.GetDeviceCategoryAsync(dto.DeviceTypeId);

            var deviceId = await _deviceRepository.CreateDeviceAsync(_mapper.Map<CreateDeviceDto>(dto), uow);

            switch (deviceCategory)
            {
                case DeviceTypeCategories.Camera:
                    await _cameraConfigurationRepository.AddDefaultCameraConfigurationToDeviceAsync(deviceId, uow);
                    break;
                case DeviceTypeCategories.Sensor:
                case DeviceTypeCategories.Alarm:
                case DeviceTypeCategories.BlindsOpener:
                default:
                    break;
            }

            return deviceId;
        }
        public async Task<bool> UpdateDeviceAsync(UpdateDeviceDto dto, IUnitOfWork? uow = null)
        {
            return await _deviceRepository.UpdateDeviceAsync(dto, uow);
        }

        public async Task<string?> GenerateSessionTokenForDeviceAsync(string ipv4, string mac)
        {
            var newSessionToken = _deviceAuthenticatorService.GenerateSessionToken();

            var result = await _deviceRepository.SetSessionTokenAsync(new SetESPSessionTokenDto()
            {
                Ipv4 = ipv4,
                Mac = mac,
                SessionToken = newSessionToken,
                ExpirationDate = DateTime.UtcNow.AddMinutes(_sessionTokenDurationInMinutes),
                AllowedStatuses = [DevicePairStatus.Paired, DevicePairStatus.Paired]
            });

            if (result)
            {
                return newSessionToken;
            }

            return null;
        }

        public async Task<List<CameraDisplayModel>> GetAllCamerasAsync()
        {
            var result = await _deviceRepository.GetAllDevicesAsync();

            return _mapper.Map<List<CameraDisplayModel>>(result);
        }

        public async Task<List<CameraDisplayModel>> GetAllCamerasAsync(params List<Guid> cameraIds)
        {
            var result = await _deviceRepository.GetAllDevicesAsync(cameraIds);

            return _mapper.Map<List<CameraDisplayModel>>(result);
        }

        public async Task<List<CameraDisplayModel>> GetAllCamerasAsync(params DevicePairStatus[] statuses)
        {
            var result = await _deviceRepository.GetAllDevicesWithStatusesAsync(statuses);

            return _mapper.Map<List<CameraDisplayModel>>(result);
        }

        public async Task<List<DeviceDto>> GetAllActiveCameraIpsAsync()
        {
            var cameras = await _deviceRepository.GetAllDevicesWithStatusesAsync(DevicePairStatus.Paired);

            return cameras;
        }

        public async Task ChangeStatusAndInvalidateCameraAsync(Guid cameraId, DevicePairStatus newStatus)
        {
            var result = await _deviceRepository.SetDeviceStatusAsync(cameraId, newStatus);

            if (!result)
            {
                throw new InvalidOperationException($"Failed to change status for camera with ID {cameraId}");
            }

            //we have to disconnect the camera in order to update the status accross services
            //and avoid the case where the camera is still considered paired in some services but not in others,
            //which can cause issues with the camera connection and pairing process
            InvalidateCamera(cameraId);
        }

        public async Task<List<NameAndIdWithStatusModel>> GetAllCameraNameAndIdAsync()
        {
            var result = await _deviceRepository.GetAllDeviceNameAndIdAsync();

            return _mapper.Map<List<NameAndIdWithStatusModel>>(result);
        }

        public async Task<List<PreprovisionDeviceModel>> GetCamerasByIdAsync(List<Guid> cameraIds)
        {
            var allCameras = await _deviceRepository.GetAllDevicesAsync();
            var filteredCameras = allCameras.Where(c => cameraIds.Contains(c.Id)).ToList();
            return _mapper.Map<List<PreprovisionDeviceModel>>(filteredCameras);
        }

        public async Task<int> GetTotalCamerasAsync(params List<DevicePairStatus> status)
        {
            var result = await _deviceRepository.GetTotalDevicesAsync(status.ToArray());
            return result;
        }

        public async Task<string?> GetDeviceNameAsync(Guid deviceId)
        {
            return await _deviceRepository.GetDeviceNameAsync(deviceId);
        }

        private void InvalidateCamera(Guid cameraId)
        {
            _cameraFramesManagerService.CloseProcessedFramesCameraChannel(cameraId);

            _activeCameraConnections.TryDisconnect(cameraId);
        }
    }
}