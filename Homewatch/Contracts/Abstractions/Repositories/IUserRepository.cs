using Homewatch.Contracts.Dtos.UserDtos;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface IUserRepository
    {
        Task<Guid> GetSecurityStampAsync(Guid userId);

        Task<bool> SetSecurityStampAsync(Guid userId, Guid securityStamp);

        Task<Guid> CreateUserAsync(CreateUserDto dto);

        Task<UserDto?> GetUserByIdAsync(Guid userId);

        Task<UserDto?> GetUserByUsernameAsync(string username);

        Task<List<UserListItemDto>> GetAllUsersAsync();

        Task<List<Guid>> GetAllActiveUserIdsAsync();

        Task<List<string>> GetEmailsByUserIdsAsync(List<Guid> userIds);

        Task<int> GetTotalUsersAsync();

        Task<bool> UpdatePasswordAsync(Guid userId, string newPasswordHash, bool clearFirstTimeSetup);

        Task<bool> DeleteUserAsync(Guid userId);

        Task<bool> DoesUserExistAsync(string userName, string email);
    }
}
