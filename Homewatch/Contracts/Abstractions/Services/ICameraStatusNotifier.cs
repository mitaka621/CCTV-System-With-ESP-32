using Homewatch.Contracts.Dtos.CameraConfigurationDtos;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface ICameraStatusNotifier
    {
        event Action<Guid>? StatusChanged;

        event Action<SensorResolutionRejectedDto>? SensorResolutionRejected;

        void NotifyStatusChanged(Guid cameraId);
    }
}
