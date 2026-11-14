using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Dtos.NotificationDtos;
using Homewatch.Contracts.Enums;
using Homewatch.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Homewatch.Core.Services.SystemServices
{
    public class NotificationsService : INotificationsService, INotificationsNotifier
    {
        private readonly IUserNotificationRepository _userNotificationRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationRepository _notificationRepository;

        private readonly ILogger<NotificationsService> _logger;
        private readonly IEmailNotificationService _emailNotificationService;

        public event Action<NotificationsChangedDto>? NotificationsChanged;

        public NotificationsService(
            IUserNotificationRepository userNotificationRepository,
            IUserRepository userRepository,
            INotificationRepository notificationRepository,
            ILogger<NotificationsService> logger,
            IEmailNotificationService emailNotificationService)
        {
            _userNotificationRepository = userNotificationRepository;
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
            _logger = logger;
            _emailNotificationService = emailNotificationService;
        }

        public void NotifyNotificationsChanged(NotificationsChangedDto notificationsChanged)
        {
            var handlers = NotificationsChanged;

            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<Action<NotificationsChangedDto>>())
            {
                try
                {
                    handler(notificationsChanged);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "NotificationsChanged handler threw for notification type {NotificationType}", notificationsChanged.NotificationType);
                }
            }
        }

        public async Task PushNotificationToAllUsersAsync(CreateNotificationDto notification)
        {
            var relativeRedirectUrl = RelativeRedirectUrlValidator.Normalize(notification.RelativeRedirectUrl);

            var notificationDto = new NotificationDto()
            {
                Name = notification.Name,
                Description = notification.Description,
                DateCreatedUTC = DateTime.UtcNow,
                Icon = notification.Icon,
                IconColor = notification.IconColor,
                NotificationType = notification.NotificationType,
                RelativeRedirectUrl = relativeRedirectUrl,
                TopicIdentifier = GenerateNotificationTopic(notification.Name, notification.Description, relativeRedirectUrl)
            };

            var usersWithAlreadyActiveNotification = notification.IsUnique
                ? await _userNotificationRepository.GetUsersWithActiveNotificationByTopicAsync(notificationDto.TopicIdentifier)
                : [];

            var allActiveUsers = await _userRepository.GetAllActiveUserIdsAsync();

            var usersToSendNotificationTo = allActiveUsers.Except(usersWithAlreadyActiveNotification).ToList();

            if (usersToSendNotificationTo.Count == 0)
            {
                return;
            }

            await _notificationRepository.CreateNotificationForUsersAsync(notificationDto, usersToSendNotificationTo);

            NotifyNotificationsChanged(new NotificationsChangedDto
            {
                UserIds = usersToSendNotificationTo,
                NotificationType = notificationDto.NotificationType,
                IsNewNotification = true,
                Notification = notificationDto
            });

            _ = _emailNotificationService.SendNotificationAsync(notificationDto, usersToSendNotificationTo);
        }

        public async Task<Dictionary<NotificationType, int>> GetActiveNotificationCountsAsync(Guid userId)
        {
            return await _userNotificationRepository.GetActiveNotificationCountsAsync(userId);
        }

        public async Task<List<NotificationDto>> GetActiveNotificationsAsync(Guid userId, NotificationType notificationType)
        {
            return await _userNotificationRepository.GetActiveNotificationsAsync(userId, notificationType);
        }

        public async Task<NotificationPageDto> GetDismissedNotificationsAsync(Guid userId, NotificationType notificationType, int skip, int take)
        {
            return new NotificationPageDto
            {
                Notifications = await _userNotificationRepository.GetDismissedNotificationsAsync(userId, notificationType, skip, take),
                TotalCount = await _userNotificationRepository.GetDismissedNotificationsCountAsync(userId, notificationType)
            };
        }

        public async Task DismissNotificationAsync(Guid userId, Guid notificationId)
        {
            var notificationType = await _userNotificationRepository.DismissNotificationAsync(userId, notificationId);

            if (notificationType == null)
            {
                return;
            }

            NotifyDismissed(userId, notificationType.Value);
        }

        public async Task DismissAllNotificationsAsync(Guid userId, NotificationType notificationType)
        {
            if (await _userNotificationRepository.DismissAllNotificationsAsync(userId, notificationType) > 0)
            {
                NotifyDismissed(userId, notificationType);
            }
        }

        private void NotifyDismissed(Guid userId, NotificationType notificationType)
        {
            NotifyNotificationsChanged(new NotificationsChangedDto
            {
                UserIds = [userId],
                NotificationType = notificationType,
                IsNewNotification = false
            });
        }

        private byte[] GenerateNotificationTopic(string name, string description, string? relativeRedirectUrl)
        {
            return $"{name}{description}{relativeRedirectUrl}".ToHash();
        }
    }
}
