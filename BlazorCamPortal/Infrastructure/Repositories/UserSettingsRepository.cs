using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class UserSettingsRepository : IUserSettingsRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public UserSettingsRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper)
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
