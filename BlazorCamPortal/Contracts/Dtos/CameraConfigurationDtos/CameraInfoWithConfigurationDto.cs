using CamPortal.Contracts.Dtos.CameraDtos;

namespace CamPortal.Contracts.Dtos.CameraConfigurationDtos
{
    public class CameraInfoWithConfigurationDto : DeviceDto
    {
        public CameraStreamingConfigurationDto Configuration { get; set; } = null!;
    }
}
