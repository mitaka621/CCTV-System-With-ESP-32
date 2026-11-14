using Homewatch.Contracts.Dtos.NotificationDtos;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface INotificationsNotifier
    {
        event Action<NotificationsChangedDto>? NotificationsChanged;
    }
}
