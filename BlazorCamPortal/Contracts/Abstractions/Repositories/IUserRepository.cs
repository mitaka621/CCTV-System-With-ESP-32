using CamPortal.Contracts.Dtos.UserDtos;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface IUserRepository
    {
        Task<Guid> GetSecurityStampAsync(Guid userId);

        Task<bool> SetSecurityStampAsync(Guid userId, Guid securityStamp);

        Task<Guid> CreateUserAsync(CreateUserDto dto);

        Task<UserDto?> GetUserByIdAsync(Guid userId);

        Task<UserDto?> GetUserByUsernameAsync(string username);

        Task<List<UserListItemDto>> GetAllUsersAsync();

        Task<bool> UpdatePasswordAsync(Guid userId, string newPasswordHash, bool clearFirstTimeSetup);

        Task<bool> DeleteUserAsync(Guid userId);

        Task<bool> DoesUserExistAsync(string userName, string email);
    }
}
