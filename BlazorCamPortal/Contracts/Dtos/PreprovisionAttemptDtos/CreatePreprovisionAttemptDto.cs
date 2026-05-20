using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.PreprovisionAttemptDtos
{
    public class CreatePreprovisionAttemptDto
    {
        public Guid DeviceId { get; set; }

        public required string Nonce { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        public PreprovisionStatus PreprovisionStatus { get; set; }

        public string? ExpectedNetworkIpv4 { get; set; }

        public string? ExpectedSubnetMask { get; set; }
    }
}
