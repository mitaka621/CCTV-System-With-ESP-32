namespace Homewatch.Contracts.Dtos.RemoteStorageDtos
{
    public class VideoChunkReplicationItemDto
    {
        public Guid VideoChunkId { get; set; }

        public Guid DeviceId { get; set; }

        public required string FileName { get; set; }

        public DateTime ChunkEndTime { get; set; }
    }
}
