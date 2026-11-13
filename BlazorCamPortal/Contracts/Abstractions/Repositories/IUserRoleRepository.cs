using Homewatch.Contracts.Dtos.UserDtos;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface IUserRoleRepository
    {
        Task<List<RoleDto>> GetAllRolesAsync();

        Task<bool> ReplaceRolesForUserAsync(Guid userId, List<Guid> roleIds);

        Task<int> CountUsersInRoleAsync(string roleName);
    }
}
