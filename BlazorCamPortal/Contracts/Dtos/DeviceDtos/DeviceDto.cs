using CamPortal.Contracts.Dtos.PreprovisionAttemptDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.CameraDtos
{
    public class DeviceDto
    {
        public Guid Id { get; set; }

        public Guid DeviceTypeId { get; set; }

        public string? Name { get; set; }

        public string? Ipv4Address { get; set; }

        public DevicePairStatus PairStatus { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public required string PublicKey { get; set; }

        public required string Fingerprint { get; set; }

        public List<PreprovisionAttemptDto> PreprovisionAttempts { get; set; } = new();
    }
}
