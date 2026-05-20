using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.PreprovisionAttemptDtos
{
    public class PreprovisionAttemptDto
    {
        public Guid Id { get; set; }

        public Guid DeviceId { get; set; }

        public required string Nonce { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        public DateTime? ClaimedAt { get; set; }

        public string? ClaimedFromIpv4 { get; set; }

        public string? ExpectedNetworkIpv4 { get; set; }

        public string? ExpectedSubnetMask { get; set; }

        public PreprovisionStatus PreprovisionStatus { get; set; }

        public int RemainingAttempts { get; set; }
    }
}
