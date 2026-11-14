using Homewatch.Contracts.Dtos.NotificationDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface INotificationsService
    {

        Task PushNotificationToAllUsersAsync(CreateNotificationDto notification);

        Task<Dictionary<NotificationType, int>> GetActiveNotificationCountsAsync(Guid userId);

        Task<List<NotificationDto>> GetActiveNotificationsAsync(Guid userId, NotificationType notificationType);

        Task<NotificationPageDto> GetDismissedNotificationsAsync(Guid userId, NotificationType notificationType, int skip, int take);

        Task DismissNotificationAsync(Guid userId, Guid notificationId);

        Task DismissAllNotificationsAsync(Guid userId, NotificationType notificationType);
    }
}
