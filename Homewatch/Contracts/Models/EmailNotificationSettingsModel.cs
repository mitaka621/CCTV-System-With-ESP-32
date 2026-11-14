using System.ComponentModel.DataAnnotations;

namespace Homewatch.Contracts.Models
{
    public class EmailNotificationSettingsModel
    {
        [Required(ErrorMessage = "Gmail address is required.")]
        [EmailAddress(ErrorMessage = "Gmail address is not a valid email address.")]
        [StringLength(320)]
        public string SenderAddress { get; set; } = string.Empty;

        [StringLength(100)]
        public string SenderDisplayName { get; set; } = "Homewatch";

        [StringLength(100)]
        public string AppPassword { get; set; } = string.Empty;

        public bool HasStoredAppPassword { get; set; }

        [StringLength(500)]
        public string AppBaseUrl { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }
    }
}
