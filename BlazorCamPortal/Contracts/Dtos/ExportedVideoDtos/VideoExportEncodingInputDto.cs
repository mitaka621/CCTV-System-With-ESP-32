namespace CamPortal.Contracts.Dtos.ExportedVideoDtos
{
    public class VideoExportEncodingInputDto
    {
        public Guid ExportId { get; set; }

        public Guid UserId { get; set; }

        public double TotalSeconds { get; set; }

        public required string ConcatListPath { get; set; }

        public required string OutputPath { get; set; }
    }
}
