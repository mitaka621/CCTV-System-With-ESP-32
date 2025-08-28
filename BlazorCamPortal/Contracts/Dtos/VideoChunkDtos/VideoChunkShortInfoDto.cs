namespace BlazorCamPortal.Contracts.Dtos.VideoChunkDtos
{
    public class VideoChunkShortInfoDto
    {
        public required string FileName { get; set; }

        public DateTime ChunkStartTime { get; set; }

        public DateTime ChunkEndTime { get; set; }
    }
}
