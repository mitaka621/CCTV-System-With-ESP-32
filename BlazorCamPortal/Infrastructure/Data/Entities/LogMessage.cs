using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Infrastructure.Data.Entities
{
    [Index(nameof(TimestampUTC))]
    public class LogMessage
    {
        public const int MaxCategoryLength = 100;
        public const int MaxMessageLength = 4000;
        public const int MaxExceptionLength = 8000;

        [Key]
        public Guid Id { get; set; }

        [Required]
        public DateTime TimestampUTC { get; set; }

        [Required]
        public LogLevel LogLevel { get; set; }

        [Required]
        [StringLength(MaxCategoryLength)]
        public required string Category { get; set; }

        [Required]
        [StringLength(MaxMessageLength)]
        public required string Message { get; set; }

        [StringLength(MaxExceptionLength)]
        public string? Exception { get; set; }
    }
}
