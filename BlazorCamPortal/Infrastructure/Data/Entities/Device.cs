using CamPortal.Contracts.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CamPortal.Infrastructure.Data.Entities
{
    [Index(nameof(PairStatus))]
    public class Device
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid DeviceTypeId { get; set; }
        [ForeignKey(nameof(DeviceTypeId))]
        public DeviceType DeviceType { get; set; } = null!;

        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(20)]
        public string? Ipv4Address { get; set; }

        [Required]
        public DevicePairStatus PairStatus { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(2000)]
        public string? SessionToken { get; set; }

        public DateTime? SessionTokenExpirationDate { get; set; }

        [MaxLength(100)]
        public string? FirmwareVersion { get; set; }

        [Required]
        [MaxLength(2000)]
        public required string PublicKey { get; set; }

        [Required]
        [MaxLength(2000)]
        public required string Fingerprint { get; set; }

        public List<PreprovisionAttempt> PreprovisionAttempts { get; set; } = new();

        public CameraConfiguration? CameraConfiguration { get; set; }
    }
}
