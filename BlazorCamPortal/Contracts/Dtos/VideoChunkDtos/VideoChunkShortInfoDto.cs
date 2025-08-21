namespace BlazorCamPortal.Contracts.Dtos.VideoChunkDtos
{
    public class VideoChunkShortInfoDto
    {
        public required string FileName { get; set; }

        public DateTime ChunkStartDate { get; set; }

        public DateTime ChunkEndDate { get; set; }
    }
}
