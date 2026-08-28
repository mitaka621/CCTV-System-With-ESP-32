using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.CameraDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Models;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;

namespace CamPortal.Core.Services.Devices
{
    public class DeviceService : IDeviceService
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly IMapper _mapper;
        private readonly ICameraFramesManagerService _cameraFramesManagerService;
        private readonly IActiveCameraConnections _activeCameraConnections;
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        private readonly ICameraConfigurationRepository _cameraConfigurationRepository;
        private readonly ICameraCommandDispatcher _cameraCommandDispatcher;
        private readonly ISmokeAlarmDetectorConfigurationRepository _smokeAlarmDetectorConfigurationRepository;
        private readonly ISmokeAlarmTelemetryRepository _smokeAlarmTelemetryRepository;
        private readonly ICameraRepository _cameraRepository;
        private readonly ISmokeAlarmRepository _smokeAlarmRepository;

        public DeviceService(
            IDeviceRepository deviceRepository,
            IMapper mapper,
            IDeviceAuthenticatorService deviceAuthenticatorService,
            IConfiguration configuration,
            ICameraFramesManagerService cameraFramesManagerService,
            IActiveCameraConnections activeCameraConnections,
            IDeviceTypeRepository deviceTypeRepository,
            ICameraConfigurationRepository cameraConfigurationRepository,
            ICameraCommandDispatcher cameraCommandDispatcher,
            ISmokeAlarmDetectorConfigurationRepository smokeAlarmDetectorConfigurationRepository,
            ISmokeAlarmTelemetryRepository smokeAlarmTelemetryRepository,
            ICameraRepository cameraRepository,
            ISmokeAlarmRepository smokeAlarmRepository)
        {
            _deviceRepository = deviceRepository;
            _mapper = mapper;
            _cameraFramesManagerService = cameraFramesManagerService;
            _activeCameraConnections = activeCameraConnections;
            _deviceTypeRepository = deviceTypeRepository;
            _cameraConfigurationRepository = cameraConfigurationRepository;
            _cameraCommandDispatcher = cameraCommandDispatcher;
            _smokeAlarmDetectorConfigurationRepository = smokeAlarmDetectorConfigurationRepository;
            _smokeAlarmTelemetryRepository = smokeAlarmTelemetryRepository;
            _cameraRepository = cameraRepository;
            _smokeAlarmRepository = smokeAlarmRepository;
        }


        public async Task<Guid> CreateDeviceAsync(CreateDeviceDto dto, IUnitOfWork? uow = null)
        {
            var deviceCategory = await _deviceTypeRepository.GetDeviceCategoryAsync(dto.DeviceTypeId);

            if (string.IsNullOrEmpty(dto.Name))
            {
                var deviceCount = await _deviceRepository.CountAllDevicesFromCategoryAsync(deviceCategory);
                dto.Name = $"{deviceCategory}_{deviceCount + 1}";
            }

            var deviceId = await _deviceRepository.CreateDeviceAsync(dto, uow);

            switch (deviceCategory)
            {
                case DeviceTypeCategories.Camera:
                    await _cameraConfigurationRepository.AddDefaultCameraConfigurationToDeviceAsync(deviceId, uow);
                    break;
                case DeviceTypeCategories.SmokeAlarm:
                    await _smokeAlarmDetectorConfigurationRepository.AddDefaultSmokeAlarmConfigurationToDeviceAsync(deviceId, uow);
                    break;
                default:
                    break;
            }

            return deviceId;
        }
        public async Task<bool> UpdateDeviceAsync(UpdateDeviceDto dto, IUnitOfWork? uow = null)
        {
            return await _deviceRepository.UpdateDeviceAsync(dto, uow);
        }

        public async Task<DevicesByTypeDisplayModel> GetAllDevicesByTypeAsync(DeviceFilterModel filterModel)
        {
            if (!MiscUtilities.ValidateModel(filterModel))
                filterModel = new();

            var result = new DevicesByTypeDisplayModel();

            foreach (var category in Enum.GetValues<DeviceTypeCategories>())
            {
                switch (category)
                {
                    case DeviceTypeCategories.Camera:
                        var cameras = await _cameraRepository.GetAllCamerasWithTypeAndConfigurationAsync(filterModel);

                        result.Cameras = cameras.GroupBy(x => x.DeviceType)
                            .ToDictionary(g => _mapper.Map<DeviceTypeDisplayModel>(g.Key), g => g.Select(x => _mapper.Map<CameraDisplayModel>(x)).ToList());
                        break;
                    case DeviceTypeCategories.SmokeAlarm:
                        var smokeAlarms = await _smokeAlarmRepository.GetAllSmokeAlarmsWithTypeAndLatestTelemetryAsync(filterModel);

                        result.SmokeAlarms = smokeAlarms.GroupBy(x => x.DeviceType)
                            .ToDictionary(g => _mapper.Map<DeviceTypeDisplayModel>(g.Key), g => g.Select(x => new SmokeAlarmDisplayModel()
                            {
                                BatterySOCPercent = x.LatestTelemetry.BatterySOCPercent,
                                BatteryVoltage = x.LatestTelemetry.BatteryVoltage,
                                BootCount = x.LatestTelemetry.BootCount,
                                CreatedAt = x.CreatedAt,
                                Fingerprint = x.Fingerprint,
                                Id = x.Id,
                                Ipv4Address = x.Ipv4Address,
                                Name = x.Name,
                                LatestCommunicationTime = x.LatestTelemetry.LoggedTimeUTC,
                                PairStatus = x.PairStatus,
                                UpdatedAt = x.UpdatedAt
                            }).ToList());
                        break;
                    default:
                        throw new NotImplementedException($"Device category {category} is not implemented in GetAllDevicesByTypeAsync.");
                }
            }

            return result;
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

        public async Task<List<DeviceWithPreprovisionAttemptsDto>> GetAllActiveCameraIpsAsync()
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

            if (newStatus == DevicePairStatus.Removed)
            {
                _cameraCommandDispatcher.RemoveCamera(cameraId);
            }
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

        public async Task<bool> DeleteDeviceAsync(Guid deviceId)
        {
            InvalidateCamera(deviceId);
            _cameraCommandDispatcher.RemoveCamera(deviceId);
            return await _deviceRepository.DeleteDeviceAsync(deviceId);
        }

        private void InvalidateCamera(Guid cameraId)
        {
            _cameraFramesManagerService.CloseProcessedFramesCameraChannel(cameraId);

            _activeCameraConnections.TryDisconnect(cameraId);
        }
    }
}