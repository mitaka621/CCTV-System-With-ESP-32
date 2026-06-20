namespace CamPortal.Contracts.Dtos.VideoChunkDtos
{
    public class ExpiredVideoChunkDto
    {
        public Guid Id { get; set; }

        public Guid DeviceId { get; set; }

        public required string FileName { get; set; }
    }
}
