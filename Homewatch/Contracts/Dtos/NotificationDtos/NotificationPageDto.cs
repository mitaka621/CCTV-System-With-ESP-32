namespace Homewatch.Contracts.Dtos.NotificationDtos
{
    public class NotificationPageDto
    {
        public List<NotificationDto> Notifications { get; set; } = new();

        public int TotalCount { get; set; }
    }
}
