using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Models
{
    public class CameraDisplayModel
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }

        public string? Ipv4Address { get; set; }

        public DevicePairStatus PairStatus { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? Fingerprint { get; set; }

        public string? FirmwareVersion { get; set; }

        public int? ResolutionWidth { get; set; }

        public int? ResolutionHeight { get; set; }

        public CameraAspectRatios? AspectRatio { get; set; }
    }
}
