using Homewatch.Contracts.Dtos.SecurityDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Dtos.DeviceDtos
{
    public class OutboundDeviceMessageDto
    {
        public DeviceCommand Command { get; init; }

        public List<DeviceEspConfigDto>? Config { get; init; }
    }
}
