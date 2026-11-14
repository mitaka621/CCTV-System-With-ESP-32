using Homewatch.Contracts.Dtos.NotificationDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface IUserNotificationRepository
    {
        Task<List<Guid>> GetUsersWithActiveNotificationByTopicAsync(byte[] topic);

        Task<Dictionary<NotificationType, int>> GetActiveNotificationCountsAsync(Guid userId);

        Task<List<NotificationDto>> GetActiveNotificationsAsync(Guid userId, NotificationType notificationType);

        Task<int> GetDismissedNotificationsCountAsync(Guid userId, NotificationType notificationType);

        Task<List<NotificationDto>> GetDismissedNotificationsAsync(Guid userId, NotificationType notificationType, int skip, int take);

        Task<NotificationType?> DismissNotificationAsync(Guid userId, Guid notificationId);

        Task<int> DismissAllNotificationsAsync(Guid userId, NotificationType notificationType);
    }
}
