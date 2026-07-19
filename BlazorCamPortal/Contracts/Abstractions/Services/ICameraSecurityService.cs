using CamPortal.Contracts.Dtos.SecurityDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraSecurityService
    {
        Task ArmAsync(Guid cameraId, bool armed);

        Task SetCaseSensorInstalledAsync(Guid cameraId, bool installed);

        Task SetMovementThresholdsAsync(Guid cameraId, double movementThresholdOffset, double rotationThresholdOffset);

        Task TriggerAsync(Guid cameraId);

        Task ResetAsync(Guid cameraId);

        Task RefreshThresholdsAsync();

        CameraSecurityStatusDto GetStatus(Guid cameraId);
    }
}
