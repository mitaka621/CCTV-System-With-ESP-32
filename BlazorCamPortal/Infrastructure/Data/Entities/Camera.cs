using System.ComponentModel.DataAnnotations;
using CamPortal.Contracts.Enums;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Data.Entities
{
    [Index(nameof(Ipv4Address))]
    public class Camera
    {
        [Key]
        public Guid Id { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        [Required]
        [MaxLength(20)]
        public required string Ipv4Address { get; set; }

        [Required]
        [MaxLength(20)]
        public required string MacAddress { get; set; }

        [Required]
        public PairStatus PairStatus { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(2000)]
        public string? SessionToken { get; set; }

        public DateTime? SessionTokenExpirationDate { get; set; }
    }
}
