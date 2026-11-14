using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Dtos.NotificationDtos;
using Homewatch.Contracts.Enums;
using Homewatch.Infrastructure.Data;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace Homewatch.Infrastructure.Repositories
{
    public class NotificationUserRepository : IUserNotificationRepository
    {
        private readonly IDbContextFactory<HomewatchDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public NotificationUserRepository(IDbContextFactory<HomewatchDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task<List<Guid>> GetUsersWithActiveNotificationByTopicAsync(byte[] topic)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await dbContext.NotificationsUsers
                .Where(x => !x.IsDismissed && x.Notication.TopicIdentifier == topic)
                .Select(x => x.UserId)
                .ToListAsync();
        }

        public async Task<Dictionary<NotificationType, int>> GetActiveNotificationCountsAsync(Guid userId)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await dbContext.NotificationsUsers
                .AsNoTracking()
                .Where(x => x.UserId == userId && !x.IsDismissed)
                .GroupBy(x => x.Notication.NotificationType)
                .Select(x => new { NotificationType = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.NotificationType, x => x.Count);
        }

        public async Task<List<NotificationDto>> GetActiveNotificationsAsync(Guid userId, NotificationType notificationType)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await QueryUserNotifications(dbContext, userId, notificationType, isDismissed: false)
                .ToListAsync();
        }

        public async Task<int> GetDismissedNotificationsCountAsync(Guid userId, NotificationType notificationType)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await dbContext.NotificationsUsers
                .CountAsync(x => x.UserId == userId && x.IsDismissed && x.Notication.NotificationType == notificationType);
        }

        public async Task<List<NotificationDto>> GetDismissedNotificationsAsync(Guid userId, NotificationType notificationType, int skip, int take)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await QueryUserNotifications(dbContext, userId, notificationType, isDismissed: true)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<NotificationType?> DismissNotificationAsync(Guid userId, Guid notificationId)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var userNotificationQuery = dbContext.NotificationsUsers
                .Where(x => x.UserId == userId && x.NotificationId == notificationId && !x.IsDismissed);

            var notificationType = await userNotificationQuery
                .Select(x => (NotificationType?)x.Notication.NotificationType)
                .FirstOrDefaultAsync();

            if (notificationType == null)
            {
                return null;
            }

            await userNotificationQuery.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDismissed, true));

            return notificationType;
        }

        public async Task<int> DismissAllNotificationsAsync(Guid userId, NotificationType notificationType)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await dbContext.NotificationsUsers
                .Where(x => x.UserId == userId && !x.IsDismissed && x.Notication.NotificationType == notificationType)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDismissed, true));
        }

        private IQueryable<NotificationDto> QueryUserNotifications(
            HomewatchDBContext dbContext,
            Guid userId,
            NotificationType notificationType,
            bool isDismissed)
        {
            return dbContext.NotificationsUsers
                .AsNoTracking()
                .Where(x => x.UserId == userId
                    && x.IsDismissed == isDismissed
                    && x.Notication.NotificationType == notificationType)
                .OrderByDescending(x => x.Notication.DateCreatedUTC)
                .ThenBy(x => x.NotificationId)
                .Select(x => new NotificationDto
                {
                    Id = x.Notication.Id,
                    Name = x.Notication.Name,
                    Description = x.Notication.Description,
                    Icon = x.Notication.Icon,
                    IconColor = x.Notication.IconColor,
                    RelativeRedirectUrl = x.Notication.RelativeRedirectUrl,
                    DateCreatedUTC = x.Notication.DateCreatedUTC,
                    NotificationType = x.Notication.NotificationType,
                    TopicIdentifier = x.Notication.TopicIdentifier
                });
        }
    }
}
