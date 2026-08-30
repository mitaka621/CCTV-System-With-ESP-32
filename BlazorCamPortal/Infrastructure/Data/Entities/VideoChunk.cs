using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CamPortal.Infrastructure.Data.Entities
{
    [Index(nameof(DeviceId), nameof(ChunkStartTime))]
    public class VideoChunk
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public required string FileName { get; set; }

        [Required]
        public DateTime ChunkStartTime { get; set; }

        [Required]
        public DateTime ChunkEndTime { get; set; }

        [Required]
        public Guid DeviceId { get; set; }

        [ForeignKey(nameof(DeviceId))]
        public required Device Device { get; set; }

        [Required]
        public double SizeInMB { get; set; }
    }
}
