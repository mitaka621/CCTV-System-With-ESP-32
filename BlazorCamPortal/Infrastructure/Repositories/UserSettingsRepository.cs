using MapsterMapper;
using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Homewatch.Infrastructure.Repositories
{
    public class UserSettingsRepository : IUserSettingsRepository
    {
        private readonly IDbContextFactory<HomewatchDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public UserSettingsRepository(IDbContextFactory<HomewatchDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task<int?> GetNumberOfCamerasPerRowForLiveGridAsync(Guid userId)
        {
            var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.UserSettings
                 .Where(x => x.UserId == userId)
                 .Select(x => x.NumberOfCamerasPerRowForLiveGrid)
                 .FirstOrDefaultAsync();
        }

        public async Task<bool> SetNumberOfCamerasPerRowForLiveGridAsync(Guid userId, int NumberOfCamerasPerRow)
        {
            var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.UserSettings
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.NumberOfCamerasPerRowForLiveGrid, NumberOfCamerasPerRow)) > 0;
        }

    }
}
