using CamPortal.Contracts.Dtos.CameraDtos;

namespace CamPortal.Contracts.Dtos.DeviceDtos
{
    public class UpdateDeviceDto : CreateDeviceDto
    {
        public Guid Id { get; set; }
    }
}
