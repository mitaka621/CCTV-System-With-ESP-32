using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos;
using CamPortal.Contracts.Models;
using CamPortal.Core.Utilities;

namespace CamPortal.Core.Services.Devices
{
    public class SmokeAlarmDetectorConfigurationService : ISmokeAlarmDetectorConfigurationService
    {
        private readonly ISmokeAlarmDetectorConfigurationRepository _smokeAlarmDetectorConfigurationRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IMapper _mapper;

        public SmokeAlarmDetectorConfigurationService(ISmokeAlarmDetectorConfigurationRepository smokeAlarmDetectorConfigurationRepository, IDeviceRepository deviceRepository, IMapper mapper)
        {
            _smokeAlarmDetectorConfigurationRepository = smokeAlarmDetectorConfigurationRepository;
            _deviceRepository = deviceRepository;
            _mapper = mapper;
        }

        public async Task<bool> UpdateConfigurationAsync(SmokeAlarmConfigurationModel model)
        {
            if (!MiscUtilities.ValidateModel(model))
            {
                return false;
            }

            var deviceNameUpdate = await _deviceRepository.SetDeviceNameAsync(model.DeviceId, model.DeviceName);

            var cameraConfigUpdate = await _smokeAlarmDetectorConfigurationRepository.UpdateConfigurationAsync(model.DeviceId, _mapper.Map<SmokeAlarmConfigurationDto>(model));

            var result = deviceNameUpdate && cameraConfigUpdate;

            return result;
        }

        public async Task<SmokeAlarmConfigurationModel> GetSmokeAlarmConfigurationAsync(Guid deviceId)
        {
            var configDto = await _smokeAlarmDetectorConfigurationRepository.GetSmokeAlarmConfigurationAsync(deviceId);
            var model = _mapper.Map<SmokeAlarmConfigurationModel?>(configDto) ?? new SmokeAlarmConfigurationModel()
            {
                DeviceId = deviceId,
            };

            model.DeviceName = await _deviceRepository.GetDeviceNameAsync(deviceId) ?? string.Empty;

            return model;
        }
    }
}
