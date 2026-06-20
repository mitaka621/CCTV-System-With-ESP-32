using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.ExportedVideoDtos
{
    public class ExportedVideoDto
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public Guid CameraId { get; set; }

        public string? CameraName { get; set; }

        public string? ExportedURLForDownload { get; set; }

        public DateTime ExportStartedDate { get; set; }

        public DateTime ExportFinishedDate { get; set; }

        public ExportVideoStatuses ExportStatus { get; set; }

        public DateTime VideoStartDate { get; set; }

        public DateTime VideoEndDate { get; set; }

        public int SizeInMB { get; set; }
    }
}
