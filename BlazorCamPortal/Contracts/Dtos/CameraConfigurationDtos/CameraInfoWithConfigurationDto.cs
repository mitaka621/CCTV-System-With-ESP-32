using Homewatch.Contracts.Dtos.CameraDtos;

namespace Homewatch.Contracts.Dtos.CameraConfigurationDtos
{
    public class CameraInfoWithConfigurationDto : DeviceWithPreprovisionAttemptsDto
    {
        public CameraStreamingConfigurationDto Configuration { get; set; } = null!;
    }
}
