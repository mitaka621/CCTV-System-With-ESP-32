using Homewatch.Contracts.Abstractions.UnitOfWork;
using Homewatch.Contracts.Dtos.CameraConfigurationDtos;
using Homewatch.Contracts.Dtos.SecurityDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface ICameraConfigurationRepository
    {
        Task<Dictionary<Guid, CameraAspectRatios>> GetCameraAspectRatiosAsync(IReadOnlyList<Guid> cameraIds);

        Task AddDefaultCameraConfigurationToDeviceAsync(Guid deviceId, IUnitOfWork? uow = null);

        Task<bool> UpdateDeviceConfigurationAsync(Guid deviceId, CameraStreamingConfigurationDto dto);

        Task<CameraStreamingConfigurationDto?> GetCameraConfigurationAsync(Guid deviceId);

        Task<bool> SetSecurityArmedAsync(Guid deviceId, bool armed);

        Task<bool> SetCaseSensorInstalledAsync(Guid deviceId, bool installed);

        Task<bool> SetMovementThresholdsAsync(Guid deviceId, double movementThresholdOffset, double rotationThresholdOffset);

        Task<bool> SetSensorResolutionAsync(Guid deviceId, CameraSensorResolutions sensorResolution);

        Task<DeviceSecurityConfigDto?> GetDeviceSecurityConfigAsync(Guid deviceId);
    }
}
