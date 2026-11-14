using System.ComponentModel.DataAnnotations;

namespace Homewatch.Infrastructure.Data.Entities
{
    public class EmailNotificationSettings
    {
        [Key]
        public Guid Id { get; set; }

        [MaxLength(320)]
        public string SenderAddress { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SenderDisplayName { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string ProtectedAppPassword { get; set; } = string.Empty;

        [MaxLength(500)]
        public string PortalBaseUrl { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }
    }
}
