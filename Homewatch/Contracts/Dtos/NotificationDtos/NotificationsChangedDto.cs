using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Dtos.NotificationDtos
{
    public class NotificationsChangedDto
    {
        public List<Guid> UserIds { get; set; } = new();

        public NotificationType NotificationType { get; set; }

        public bool IsNewNotification { get; set; }

        public NotificationDto? Notification { get; set; }
    }
}
