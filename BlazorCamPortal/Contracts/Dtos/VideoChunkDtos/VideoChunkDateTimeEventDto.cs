namespace CamPortal.Contracts.Dtos.VideoChunkDtos
{
    public class VideoChunkDateTimeEventDto
    {
        public DateTime EventStartTime { get; set; }

        public DateTime EventEndTime { get; set; }

        public double TotalDuration => (EventEndTime - EventStartTime).TotalSeconds;
    }
}
