using CamPortal.Contracts.Dtos.SecurityDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraCommandDispatcher
    {
        bool TryEnqueueCommand(Guid cameraId, DeviceCommand command);

        bool TryEnqueueConfig(Guid cameraId, params List<DeviceEspConfigDto> config);

        void RemoveCamera(Guid cameraId);
    }
}
