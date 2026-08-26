using CamPortal.Contracts.Dtos.SecurityDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.DeviceDtos
{
    public class OutboundDeviceMessageDto
    {
        public DeviceCommand Command { get; init; }

        public DeviceEspConfigDto? Config { get; init; }
    }
}
