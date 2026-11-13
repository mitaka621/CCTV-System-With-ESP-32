using Homewatch.Contracts.Dtos.CameraConfigurationDtos;

namespace Homewatch.Contracts.Dtos.DeviceDtos
{
    public class CameraDto : DeviceDto
    {
        public CameraStreamingConfigurationDto CameraConfiguration { get; set; } = null!;
    }
}
