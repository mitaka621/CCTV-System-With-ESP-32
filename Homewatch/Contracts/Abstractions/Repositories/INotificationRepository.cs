using Homewatch.Contracts.Dtos.NotificationDtos;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface INotificationRepository
    {
        Task CreateNotificationForUsersAsync(NotificationDto notificationDto, List<Guid> userIds);

        Task<int> DeleteDismissedNotificationsOlderThanAsync(DateTime cutoffUtc);
    }
}
