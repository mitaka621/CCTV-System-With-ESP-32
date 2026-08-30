using CamPortal.Contracts.Dtos.SecurityDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ISmokeAlarmCommandDispatcher
    {
        bool TryEnqueueConfig(Guid cameraId, params List<DeviceEspConfigDto> config);
    }
}
