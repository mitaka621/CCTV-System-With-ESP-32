namespace BlazorCamPortal.Contracts.Dtos.VideoChunkDtos
{
    public class CreateVideoChunkDto
    {
        public required string FileName { get; set; }

        public DateTime ChunkStartTime { get; set; }

        public DateTime ChunkEndTime { get; set; }

        public Guid CameraId { get; set; }

        public double SizeInMB { get; set; }
    }
}
