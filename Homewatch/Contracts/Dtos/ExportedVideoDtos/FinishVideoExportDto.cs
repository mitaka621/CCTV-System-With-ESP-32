using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Dtos.ExportedVideoDtos
{
    public class FinishVideoExportDto
    {
        public Guid Id { get; set; }

        public ExportVideoStatuses ExportStatus { get; set; }

        public string? ExportedURLForDownload { get; set; }

        public string? FilePath { get; set; }

        public DateTime ExportFinishedDate { get; set; }

        public int SizeInMB { get; set; }
    }
}
