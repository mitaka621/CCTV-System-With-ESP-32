namespace CamPortal.Contracts.Dtos.VideoChunkDtos
{
    public class HLSPlaylistDto
    {
        public Guid CameraId { get; set; }

        public required string HLSPlaylistString { get; set; }

        public List<VideoChunkDateTimeEventDto> MissingVideoChunkEvents { get; set; } = new();

        public List<VideoChunkDateTimeEventDto> AvailableVideoChunkEvents { get; set; } = new();
    }
}
