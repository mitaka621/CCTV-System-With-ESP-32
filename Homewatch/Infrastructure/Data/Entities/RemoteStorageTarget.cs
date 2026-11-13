using Homewatch.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homewatch.Infrastructure.Data.Entities
{
    public class RemoteStorageTarget
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(260)]
        public string SharePath { get; set; } = string.Empty;

        [MaxLength(256)]
        public string Username { get; set; } = string.Empty;

        public string ProtectedPassword { get; set; } = string.Empty;

        public bool IsSyncEnabled { get; set; }

        public RetentionPeriod Retention { get; set; } = RetentionPeriod.ThreeMonths;

        public List<VideoChunkReplication> VideoChunkReplications { get; set; } = new();
    }
}
