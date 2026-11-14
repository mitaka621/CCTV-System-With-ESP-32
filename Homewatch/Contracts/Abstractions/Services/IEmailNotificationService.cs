using Homewatch.Contracts.Dtos.EmailNotificationDtos;
using Homewatch.Contracts.Dtos.NotificationDtos;
using Homewatch.Contracts.Models;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IEmailNotificationService
    {
        Task<EmailNotificationSettingsModel> GetSettingsAsync();

        Task<EmailOperationResultDto> SaveSettingsAsync(EmailNotificationSettingsModel settings);

        Task<EmailOperationResultDto> VerifyConnectionAsync(EmailNotificationSettingsModel settings);

        Task<EmailOperationResultDto> SendTestEmailAsync(EmailNotificationSettingsModel settings, Guid userId);

        Task<EmailOperationResultDto> SetEnabledAsync(bool isEnabled);

        Task<bool> SendNotificationAsync(NotificationDto notification, List<Guid> userIds, CancellationToken cancellationToken = default);
    }
}
