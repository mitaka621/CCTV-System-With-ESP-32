using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.CameraDtos
{
    public class CreateDeviceDto
    {
        public Guid DeviceTypeId { get; set; }

        public required DevicePairStatus PairStatus { get; set; }

        public DateTime CreatedAt { get; set; }

        public required string PublicKey { get; set; }

        public required string Fingerprint { get; set; }
    }
}
