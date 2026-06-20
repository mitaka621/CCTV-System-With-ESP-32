using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Constants;
using CamPortal.Contracts.Dtos.UserDtos;
using Microsoft.Extensions.Logging;

namespace CamPortal.Core.Services.Users
{
    public class UserManagementService : IUserManagementService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IUserAuthService _userAuthService;
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            IUserAuthService userAuthService,
            ILogger<UserManagementService> logger)
        {
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _userAuthService = userAuthService;
            _logger = logger;
        }

        public Task<List<UserListItemDto>> GetAllUsersAsync()
            => _userRepository.GetAllUsersAsync();

        public Task<int> GetTotalUsersAsync()
            => _userRepository.GetTotalUsersAsync();

        public Task<List<RoleDto>> GetAllRolesAsync()
            => _userRoleRepository.GetAllRolesAsync();

        public async Task<UpdateUserRolesResult> UpdateUserRolesAsync(Guid userId, List<Guid> roleIds, Guid currentUserId)
        {
            if (userId == currentUserId)
            {
                _logger.LogWarning("Blocked self role-edit attempt for user {UserId}.", userId);

                return UpdateUserRolesResult.CannotEditSelf;
            }

            var user = await _userRepository.GetUserByIdAsync(userId);

            if (user is null)
            {
                return UpdateUserRolesResult.UserNotFound;
            }

            var updated = await _userRoleRepository.ReplaceRolesForUserAsync(userId, roleIds);

            if (!updated)
            {
                return UpdateUserRolesResult.UserNotFound;
            }

            await _userAuthService.ForceLogoutAsync(userId);

            return UpdateUserRolesResult.Success;
        }

        public async Task<DeleteUserResult> DeleteUserAsync(Guid userId, Guid currentUserId)
        {
            if (userId == currentUserId)
            {
                _logger.LogWarning("Blocked self-deletion attempt for user {UserId}.", userId);

                return DeleteUserResult.CannotDeleteSelf;
            }

            var user = await _userRepository.GetUserByIdAsync(userId);

            if (user is null)
            {
                return DeleteUserResult.UserNotFound;
            }

            var isAdmin = user.Roles.Any(r => string.Equals(r.Name, Roles.Admin, StringComparison.Ordinal));

            if (isAdmin)
            {
                var adminCount = await _userRoleRepository.CountUsersInRoleAsync(Roles.Admin);

                if (adminCount <= 1)
                {
                    _logger.LogWarning("Blocked deletion of last remaining admin user {UserId}.", userId);

                    return DeleteUserResult.CannotDeleteLastAdmin;
                }
            }

            await _userAuthService.ForceLogoutAsync(userId);

            var deleted = await _userRepository.DeleteUserAsync(userId);

            return deleted ? DeleteUserResult.Success : DeleteUserResult.UserNotFound;
        }
    }
}
