namespace CamPortal.Contracts.Dtos.ExportedVideoDtos
{
    public class VideoExportProgressDto
    {
        public Guid ExportId { get; set; }

        public Guid UserId { get; set; }

        public double ProgressPercent { get; set; }

        public double EncodedSeconds { get; set; }

        public double TotalSeconds { get; set; }

        public double? EstimatedSecondsRemaining { get; set; }
    }
}
