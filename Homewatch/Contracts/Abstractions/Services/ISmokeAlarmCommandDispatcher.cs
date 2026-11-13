using Homewatch.Contracts.Dtos.SecurityDtos;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface ISmokeAlarmCommandDispatcher
    {
        bool TryEnqueueConfig(Guid cameraId, params List<DeviceEspConfigDto> config);
    }
}
