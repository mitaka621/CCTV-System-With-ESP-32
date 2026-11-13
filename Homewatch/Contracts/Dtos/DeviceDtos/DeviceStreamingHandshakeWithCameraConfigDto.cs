using Homewatch.Contracts.Dtos.CameraConfigurationDtos;

namespace Homewatch.Contracts.Dtos.DeviceDtos
{
    public class DeviceStreamingHandshakeWithCameraConfigDto : DeviceStreamingHandshakeDto
    {
        public CameraStreamingConfigurationDto CameraConfiguration { get; set; } = new();
    }
}
