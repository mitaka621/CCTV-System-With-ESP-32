using CamPortal.Contracts.Dtos.CameraConfigurationDtos;

namespace CamPortal.Contracts.Dtos.DeviceDtos
{
    public class CameraDto : DeviceDto
    {
        public CameraStreamingConfigurationDto CameraConfiguration { get; set; } = null!;
    }
}
