using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.PreprovisionAttemptDtos
{
    public class FinishPreprovisionAttemptDto
    {
        public Guid DeviceId { get; set; }

        public Guid PreprovisionAttemptId { get; set; }

        public DateTime? ClaimedAt { get; set; }

        public string? ClaimedFromIpv4 { get; set; }

        public PreprovisionStatus PreprovisionStatus { get; set; }
    }
}
