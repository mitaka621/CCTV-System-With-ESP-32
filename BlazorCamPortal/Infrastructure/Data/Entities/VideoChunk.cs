using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorCamPortal.Infrastructure.Data.Entities
{
    public class VideoChunk
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public required string FileName { get; set; }

        [Required]
        public DateTime ChunkStartDate { get; set; }

        [Required]
        public DateTime ChunkEndDate { get; set; }

        [Required]
        public Guid CameraId { get; set; }

        [ForeignKey(nameof(CameraId))]
        public required Camera Camera { get; set; }
    }
}
