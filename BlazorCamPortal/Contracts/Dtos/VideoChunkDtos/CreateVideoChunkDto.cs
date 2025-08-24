namespace BlazorCamPortal.Contracts.Dtos.VideoChunkDtos
{
    public class CreateVideoChunkDto
    {
        public required string FileName { get; set; }

        public DateTime ChunkStartDate { get; set; }

        public DateTime ChunkEndDate { get; set; }

        public Guid CameraId { get; set; }

        public double SizeInMB { get; set; }
    }
}
