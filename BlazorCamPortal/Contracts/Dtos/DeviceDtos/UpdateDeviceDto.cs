using Homewatch.Contracts.Dtos.CameraDtos;

namespace Homewatch.Contracts.Dtos.DeviceDtos
{
    public class UpdateDeviceDto : CreateDeviceDto
    {
        public Guid Id { get; set; }
    }
}
