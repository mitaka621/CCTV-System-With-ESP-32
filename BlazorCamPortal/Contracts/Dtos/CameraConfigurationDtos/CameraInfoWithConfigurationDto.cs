using CamPortal.Contracts.Dtos.CameraDtos;

namespace CamPortal.Contracts.Dtos.CameraConfigurationDtos
{
    public class CameraInfoWithConfigurationDto : DeviceWithPreprovisionAttemptsDto
    {
        public CameraStreamingConfigurationDto Configuration { get; set; } = null!;
    }
}
