using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Dtos.UserDtos;
using CamPortal.Infrastructure.Data;
using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;

        public UserRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<Guid> CreateUserAsync(CreateUserDto dto)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var entity = new User
            {
                Id = Guid.NewGuid(),
                UserName = dto.UserName,
                Email = dto.Email,
                Password = dto.Password,
                IsFirstTimeSetup = dto.IsFirstTimeSetup,
                SecurityStamp = dto.SecurityStamp,
            };

            entity.UserRoles = dto.RoleIds
                .Distinct()
                .Select(roleId => new UserRole
                {
                    UserId = entity.Id,
                    RoleId = roleId,
                })
                .ToList();

            context.Users.Add(entity);

            await context.SaveChangesAsync();

            return entity.Id;
        }

        public async Task<Guid> GetSecurityStampAsync(Guid userId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.SecurityStamp)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> SetSecurityStampAsync(Guid userId, Guid securityStamp)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Users
                 .Where(u => u.Id == userId)
                 .ExecuteUpdateAsync(u => u.SetProperty(x => x.SecurityStamp, securityStamp)) > 0;
        }

        public async Task<UserDto?> GetUserByIdAsync(Guid userId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(x => new UserDto()
                {
                    Id = x.Id,
                    Email = x.Email,
                    IsFirstTimeSetup = x.IsFirstTimeSetup,
                    Password = x.Password,
                    SecurityStamp = x.SecurityStamp,
                    UserName = x.UserName,
                    Roles = x.UserRoles.Select(ur => new RoleDto()
                    {
                        Id = ur.Role.Id,
                        Name = ur.Role.Name
                    }).ToList()
                })
                .FirstOrDefaultAsync();
        }

        public async Task<UserDto?> GetUserByUsernameAsync(string username)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Users
                .AsNoTracking()
                .Where(u => u.UserName == username)
                .Select(x => new UserDto()
                {
                    Id = x.Id,
                    Email = x.Email,
                    IsFirstTimeSetup = x.IsFirstTimeSetup,
                    Password = x.Password,
                    SecurityStamp = x.SecurityStamp,
                    UserName = x.UserName,
                    Roles = x.UserRoles.Select(ur => new RoleDto()
                    {
                        Id = ur.Role.Id,
                        Name = ur.Role.Name
                    }).ToList()
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<UserListItemDto>> GetAllUsersAsync()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Users
                .AsNoTracking()
                .OrderBy(u => u.UserName)
                .Select(x => new UserListItemDto()
                {
                    Id = x.Id,
                    UserName = x.UserName,
                    Email = x.Email,
                    IsFirstTimeSetup = x.IsFirstTimeSetup,
                    Roles = x.UserRoles.Select(ur => new RoleDto()
                    {
                        Id = ur.Role.Id,
                        Name = ur.Role.Name
                    }).ToList()
                })
                .ToListAsync();
        }

        public async Task<bool> UpdatePasswordAsync(Guid userId, string newPasswordHash, bool clearFirstTimeSetup)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var newSecurityStamp = Guid.NewGuid();

            if (clearFirstTimeSetup)
            {
                return await context.Users
                    .Where(u => u.Id == userId)
                    .ExecuteUpdateAsync(u => u
                        .SetProperty(x => x.Password, newPasswordHash)
                        .SetProperty(x => x.IsFirstTimeSetup, false)
                        .SetProperty(x => x.SecurityStamp, newSecurityStamp)) > 0;
            }

            return await context.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(x => x.Password, newPasswordHash)
                    .SetProperty(x => x.SecurityStamp, newSecurityStamp)) > 0;
        }

        public async Task<bool> DeleteUserAsync(Guid userId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Users
                .Where(u => u.Id == userId)
                .ExecuteDeleteAsync() > 0;
        }

        public async Task<bool> DoesUserExistAsync(string userName, string email)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserName == userName || u.Email == email);
        }
    }
}
