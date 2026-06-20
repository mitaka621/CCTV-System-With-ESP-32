namespace CamPortal.Contracts.Dtos.VideoChunkDtos
{
    public class VideoChunkShortInfoDto
    {
        public required string FileName { get; set; }

        public string? CameraFolder { get; set; }

        public DateTime ChunkStartTime { get; set; }

        public DateTime ChunkEndTime { get; set; }
    }
}
