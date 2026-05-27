using CamPortal.Contracts.Dtos.UserDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IUserManagementService
    {
        Task<List<UserListItemDto>> GetAllUsersAsync();

        Task<List<RoleDto>> GetAllRolesAsync();

        Task<UpdateUserRolesResult> UpdateUserRolesAsync(Guid userId, List<Guid> roleIds, Guid currentUserId);

        Task<DeleteUserResult> DeleteUserAsync(Guid userId, Guid currentUserId);
    }
}
