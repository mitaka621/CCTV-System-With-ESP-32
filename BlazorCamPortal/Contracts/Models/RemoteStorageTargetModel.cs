using CamPortal.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Models
{
    public class RemoteStorageTargetModel
    {
        public Guid? Id { get; set; }

        [Required]
        [StringLength(1000, MinimumLength = 3)]
        public string SharePath { get; set; } = null!;

        [StringLength(1000)]
        public string Username { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Password { get; set; } = string.Empty;

        public bool HasStoredPassword { get; set; }

        public bool IsSyncEnabled { get; set; }

        public RetentionPeriod Retention { get; set; }

        public bool IsVerifying { get; set; }

        public bool? VerificationSucceeded { get; set; }

        public string? VerificationMessage { get; set; }
    }
}
