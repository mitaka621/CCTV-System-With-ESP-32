using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Dtos.NotificationDtos;
using Homewatch.Infrastructure.Data;
using Homewatch.Infrastructure.Data.Entities;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace Homewatch.Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IDbContextFactory<HomewatchDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public NotificationRepository(IDbContextFactory<HomewatchDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task CreateNotificationForUsersAsync(NotificationDto notificationDto, List<Guid> userIds)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var entity = _mapper.Map<Notification>(notificationDto);

            entity.NotificationUsers.AddRange(userIds.Select(x => new UserNotification() { UserId = x }));

            dbContext.Notifications
                .Add(entity);

            await dbContext.SaveChangesAsync();
        }

        public async Task<int> DeleteDismissedNotificationsOlderThanAsync(DateTime cutoffUtc)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await dbContext.Notifications
                .Where(x => x.DateCreatedUTC < cutoffUtc && !x.NotificationUsers.Any(y => !y.IsDismissed))
                .ExecuteDeleteAsync();
        }
    }
}
