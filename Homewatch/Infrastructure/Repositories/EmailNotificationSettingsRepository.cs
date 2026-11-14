using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Dtos.EmailNotificationDtos;
using Homewatch.Infrastructure.Data;
using Homewatch.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Homewatch.Infrastructure.Repositories
{
    public class EmailNotificationSettingsRepository : IEmailNotificationSettingsRepository
    {
        private readonly IDbContextFactory<HomewatchDBContext> _dbContextFactory;

        public EmailNotificationSettingsRepository(IDbContextFactory<HomewatchDBContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<EmailNotificationSettingsDto?> GetSettingsAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.EmailNotificationSettings
                .AsNoTracking()
                .Where(x => x.Id == HomewatchDBContext.EmailNotificationSettingsId)
                .Select(x => new EmailNotificationSettingsDto
                {
                    SenderAddress = x.SenderAddress,
                    SenderDisplayName = x.SenderDisplayName,
                    ProtectedAppPassword = x.ProtectedAppPassword,
                    AppBaseUrl = x.PortalBaseUrl,
                    IsEnabled = x.IsEnabled
                })
                .FirstOrDefaultAsync();
        }

        public async Task SaveSettingsAsync(EmailNotificationSettingsDto settings)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var entity = await db.EmailNotificationSettings
                .FirstOrDefaultAsync(x => x.Id == HomewatchDBContext.EmailNotificationSettingsId);

            if (entity == null)
            {
                entity = new EmailNotificationSettings
                {
                    Id = HomewatchDBContext.EmailNotificationSettingsId
                };

                db.EmailNotificationSettings.Add(entity);
            }

            entity.SenderAddress = settings.SenderAddress;
            entity.SenderDisplayName = settings.SenderDisplayName;
            entity.PortalBaseUrl = settings.AppBaseUrl;
            entity.IsEnabled = settings.IsEnabled;

            if (!string.IsNullOrEmpty(settings.ProtectedAppPassword))
            {
                entity.ProtectedAppPassword = settings.ProtectedAppPassword;
            }

            await db.SaveChangesAsync();
        }

        public async Task<bool> SetEnabledAsync(bool isEnabled)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var updated = await db.EmailNotificationSettings
                .Where(x => x.Id == HomewatchDBContext.EmailNotificationSettingsId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsEnabled, isEnabled));

            return updated > 0;
        }
    }
}
