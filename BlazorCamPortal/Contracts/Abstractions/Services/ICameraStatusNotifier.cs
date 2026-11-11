using CamPortal.Contracts.Dtos.CameraConfigurationDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraStatusNotifier
    {
        event Action<Guid>? StatusChanged;

        event Action<SensorResolutionRejectedDto>? SensorResolutionRejected;

        void NotifyStatusChanged(Guid cameraId);
    }
}
