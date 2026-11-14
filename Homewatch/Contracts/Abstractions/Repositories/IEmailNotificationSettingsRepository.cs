using Homewatch.Contracts.Dtos.EmailNotificationDtos;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface IEmailNotificationSettingsRepository
    {
        Task<EmailNotificationSettingsDto?> GetSettingsAsync();

        Task SaveSettingsAsync(EmailNotificationSettingsDto settings);

        Task<bool> SetEnabledAsync(bool isEnabled);
    }
}
