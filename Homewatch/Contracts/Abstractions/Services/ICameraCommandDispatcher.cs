using Homewatch.Contracts.Dtos.SecurityDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface ICameraCommandDispatcher
    {
        bool TryEnqueueCommand(Guid cameraId, DeviceCommand command);

        bool TryEnqueueConfig(Guid cameraId, params List<DeviceEspConfigDto> config);

        void RemoveCamera(Guid cameraId);
    }
}
