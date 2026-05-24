using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.DeviceDtos
{
    public class DeviceStreamingHandshakeDto
    {
        public Guid Id { get; set; }

        public string? DeviceName { get; set; }

        public DevicePairStatus PairStatus { get; set; }

        public required string PublicKey { get; set; }

        public DeviceTypeCategories DeviceVariant { get; set; }

        public CameraStreamingConfigurationDto CameraStreamingConfiguration { get; set; } = null!;
    }
}
