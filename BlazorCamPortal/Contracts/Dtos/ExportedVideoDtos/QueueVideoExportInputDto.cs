namespace CamPortal.Contracts.Dtos.ExportedVideoDtos
{
    public class QueueVideoExportInputDto
    {
        public Guid UserId { get; set; }

        public Guid CameraId { get; set; }

        public DateTime VideoStartDate { get; set; }

        public DateTime VideoEndDate { get; set; }
    }
}
