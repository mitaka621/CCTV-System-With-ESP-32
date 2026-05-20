using CamPortal.Contracts.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CamPortal.Infrastructure.Data.Entities
{
    [Index(nameof(DeviceId), nameof(PreprovisionStatus))]
    public class PreprovisionAttempt
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid DeviceId { get; set; }
        [ForeignKey(nameof(DeviceId))]
        public required Device Device { get; set; }

        [Required]
        public required string Nonce { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public DateTime ExpiresAt { get; set; }

        public DateTime? ClaimedAt { get; set; }

        [MaxLength(20)]
        public string? ClaimedFromIpv4 { get; set; }

        [MaxLength(20)]
        public string? ExpectedNetworkIpv4 { get; set; }

        [MaxLength(20)]
        public string? ExpectedSubnetMask { get; set; }

        [Required]
        public PreprovisionStatus PreprovisionStatus { get; set; }

        [Required]
        public int RemainingAttempts { get; set; } = 3;
    }
}
