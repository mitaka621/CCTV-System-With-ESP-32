using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Models;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Core.Services
{
    public class CameraConfigurationService : ICameraConfigurationService
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly ICameraConfigurationRepository _cameraConfigurationRepository;
        private readonly IMapper _mapper;
        private readonly IActiveCameraConnections _activeCameraConnectionsService;

        private int _cameraResolutionHeight;
        private int _cameraResolutionWidth;

        public CameraConfigurationService(ICameraConfigurationRepository cameraConfigurationRepository, IConfiguration configuration, IMapper mapper, IDeviceRepository deviceRepository, IActiveCameraConnections activeCameraConnectionsService)
        {
            _cameraConfigurationRepository = cameraConfigurationRepository;
            _mapper = mapper;
            _deviceRepository = deviceRepository;
            _activeCameraConnectionsService = activeCameraConnectionsService;

            _cameraResolutionHeight = configuration.GetValue<int>("ESPCamera:ResolutionHeight");
            _cameraResolutionWidth = configuration.GetValue<int>("ESPCamera:ResolutionWidth");
        }

        public async Task<bool> UpdateCameraConfigurationAsync(Guid deviceId, CameraConfigurationModel model)
        {
            if (!ValidateModel(model) || model.ZoomStartX > _cameraResolutionWidth || model.ZoomStartY > _cameraResolutionHeight)
            {
                return false;
            }

            var cameraNameUpdate = await _deviceRepository.SetDeviceNameAsync(deviceId, model.CameraName);

            var cameraConfigUpdate = await _cameraConfigurationRepository.UpdateDeviceConfigurationAsync(deviceId, _mapper.Map<CameraStreamingConfigurationDto>(model));

            var isUpdateSuccessful = cameraNameUpdate && cameraConfigUpdate;

            if (isUpdateSuccessful)
            {
                _activeCameraConnectionsService.TryDisconnect(deviceId);
            }

            return isUpdateSuccessful;
        }

        public bool ValidateModel<T>(T model)
        {
            if (model == null)
            {
                return false;
            }

            var context = new ValidationContext(model);

            return Validator.TryValidateObject(model, context, null, validateAllProperties: true);
        }
    }
}
