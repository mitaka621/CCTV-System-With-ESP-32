using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Dtos.RemoteStorageDtos;
using Homewatch.Infrastructure.Data;
using Homewatch.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Homewatch.Infrastructure.Repositories
{
    public class RemoteStorageTargetRepository : IRemoteStorageTargetRepository
    {
        private readonly IDbContextFactory<HomewatchDBContext> _dbContextFactory;

        public RemoteStorageTargetRepository(IDbContextFactory<HomewatchDBContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<List<RemoteStorageTargetDto>> GetTargetsAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.RemoteStorageTargets
                .AsNoTracking()
                .OrderBy(x => x.SharePath)
                .Select(x => new RemoteStorageTargetDto
                {
                    Id = x.Id,
                    SharePath = x.SharePath,
                    Username = x.Username,
                    ProtectedPassword = x.ProtectedPassword,
                    IsSyncEnabled = x.IsSyncEnabled,
                    Retention = x.Retention
                })
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, RemoteStorageTargetDto>> GetEnabledAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.RemoteStorageTargets
                .AsNoTracking()
                .Where(x => x.IsSyncEnabled)
                .Select(x => new RemoteStorageTargetDto
                {
                    Id = x.Id,
                    SharePath = x.SharePath,
                    Username = x.Username,
                    ProtectedPassword = x.ProtectedPassword,
                    IsSyncEnabled = x.IsSyncEnabled,
                    Retention = x.Retention
                })
                .ToDictionaryAsync(x => x.Id, y => y);
        }

        public async Task<RemoteStorageTargetDto?> GetTargetAsync(Guid targetId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.RemoteStorageTargets
                .AsNoTracking()
                .Where(x => x.Id == targetId)
                .Select(x => new RemoteStorageTargetDto
                {
                    Id = x.Id,
                    SharePath = x.SharePath,
                    Username = x.Username,
                    ProtectedPassword = x.ProtectedPassword,
                    IsSyncEnabled = x.IsSyncEnabled,
                    Retention = x.Retention
                })
                .FirstOrDefaultAsync();
        }

        public async Task<bool> DoesSharePathExistAsync(string path, params List<Guid> targetIdsToExcludeInSerach)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await dbContext.RemoteStorageTargets
                .Where(x => !targetIdsToExcludeInSerach.Contains(x.Id))
                .AnyAsync(x => x.SharePath == path);
        }

        public async Task SaveTargetAsync(RemoteStorageTargetDto target)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            db.RemoteStorageTargets.Add(new RemoteStorageTarget()
            {
                Id = target.Id,
                IsSyncEnabled = target.IsSyncEnabled,
                ProtectedPassword = target.ProtectedPassword,
                Retention = target.Retention,
                SharePath = target.SharePath,
                Username = target.Username
            });

            await db.SaveChangesAsync();
        }

        public async Task UpdateTargetAsync(RemoteStorageTargetDto target)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var query = db.RemoteStorageTargets
                .Where(x => x.Id == target.Id);

            if (!string.IsNullOrEmpty(target.ProtectedPassword))
            {
                await query.ExecuteUpdateAsync(x => x.SetProperty(y => y.ProtectedPassword, target.ProtectedPassword));
            }

            await query.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsSyncEnabled, target.IsSyncEnabled)
                .SetProperty(y => y.Username, target.Username)
                .SetProperty(y => y.SharePath, target.SharePath)
                .SetProperty(y => y.Retention, target.Retention));
        }
    }
}
