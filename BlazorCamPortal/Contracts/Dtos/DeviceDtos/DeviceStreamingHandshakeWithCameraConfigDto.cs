using CamPortal.Contracts.Dtos.CameraConfigurationDtos;

namespace CamPortal.Contracts.Dtos.DeviceDtos
{
    public class DeviceStreamingHandshakeWithCameraConfigDto : DeviceStreamingHandshakeDto
    {
        public CameraStreamingConfigurationDto CameraConfiguration { get; set; } = new();
    }
}
