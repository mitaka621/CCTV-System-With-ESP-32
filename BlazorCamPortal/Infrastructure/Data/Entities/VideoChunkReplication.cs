using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CamPortal.Infrastructure.Data.Entities
{
    [Index(nameof(RemoteStorageTargetId), nameof(VideoChunkId), IsUnique = true)]
    public class VideoChunkReplication
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid RemoteStorageTargetId { get; set; }
        [ForeignKey(nameof(RemoteStorageTargetId))]
        public RemoteStorageTarget RemoteStorageTarget { get; set; } = null!;

        [Required]
        public Guid VideoChunkId { get; set; }
        [ForeignKey(nameof(VideoChunkId))]
        public VideoChunk VideoChunk { get; set; } = null!;

        public DateTime ReplicatedDateUTC { get; set; }

        public bool IsDeleted { get; set; }
    }
}
