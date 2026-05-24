namespace CamPortal.Contracts.Dtos.VideoChunkDtos
{
    public class CreateVideoChunkDto
    {
        public required string FileName { get; set; }

        public DateTime ChunkStartTime { get; set; }

        public DateTime ChunkEndTime { get; set; }

        public Guid DeviceId { get; set; }

        public double SizeInMB { get; set; }
    }
}
