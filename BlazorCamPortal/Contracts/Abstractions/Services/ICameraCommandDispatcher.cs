using CamPortal.Contracts.Dtos.SecurityDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraCommandDispatcher
    {
        bool TryEnqueueCommand(Guid cameraId, CameraCommand command);

        bool TryEnqueueConfig(Guid cameraId, DeviceEspConfigDto config);

        void RemoveCamera(Guid cameraId);
    }
}
