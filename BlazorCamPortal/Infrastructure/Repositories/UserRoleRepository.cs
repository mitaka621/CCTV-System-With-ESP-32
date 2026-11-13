using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Dtos.UserDtos;
using Homewatch.Infrastructure.Data;
using Homewatch.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Homewatch.Infrastructure.Repositories
{
    public class UserRoleRepository : IUserRoleRepository
    {
        private readonly IDbContextFactory<HomewatchDBContext> _dbContextFactory;

        public UserRoleRepository(IDbContextFactory<HomewatchDBContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<List<RoleDto>> GetAllRolesAsync()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Roles
                .AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name
                })
                .ToListAsync();
        }

        public async Task<int> CountUsersInRoleAsync(string roleName)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.UsersRoles
                .AsNoTracking()
                .Where(ur => ur.Role.Name == roleName)
                .Select(ur => ur.UserId)
                .Distinct()
                .CountAsync();
        }

        public async Task<bool> ReplaceRolesForUserAsync(Guid userId, List<Guid> roleIds)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            await using var transaction = await context.Database.BeginTransactionAsync();

            await context.UsersRoles
                .Where(ur => ur.UserId == userId)
                .ExecuteDeleteAsync();

            var distinctRoleIds = roleIds.Distinct().ToList();

            if (distinctRoleIds.Count > 0)
            {
                context.UsersRoles.AddRange(distinctRoleIds.Select(roleId => new UserRole
                {
                    UserId = userId,
                    RoleId = roleId
                }));

                await context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return true;
        }
    }
}
