using CamPortal.Contracts.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CamPortal.Infrastructure.Data.Entities
{
    [Index(nameof(ExportFinishedDate))]
    public class ExportedVideo
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        public Guid CameraId { get; set; }
        [ForeignKey(nameof(CameraId))]
        public Device Camera { get; set; } = null!;

        [StringLength(300)]
        public string? ExportedURLForDownload { get; set; }

        [StringLength(300)]
        public string? FilePath { get; set; }

        public DateTime ExportStartedDate { get; set; }

        public DateTime ExportFinishedDate { get; set; }

        public ExportVideoStatuses ExportStatus { get; set; }

        public DateTime VideoStartDate { get; set; }

        public DateTime VideoEndDate { get; set; }

        public int SizeInMB { get; set; }
    }
}
