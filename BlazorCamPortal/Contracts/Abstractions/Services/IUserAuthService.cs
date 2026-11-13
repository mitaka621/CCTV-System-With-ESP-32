using Homewatch.Contracts.Dtos.UserDtos;
using Homewatch.Contracts.Models;
using System.Security.Claims;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IUserAuthService
    {
        Task<LoginResultDto> LogInAsync(string username, string password);

        Task<ClaimsPrincipal?> BuildPrincipalAsync(Guid userId);

        Task<Guid?> CreateUserAsync(CreateUserModel model);

        Task ForceLogoutAsync(Guid userId);

        Task<bool> ChangePasswordAsync(Guid userId, string newPassword);

        Task<bool> VerifyPasswordAsync(Guid userId, string? password);
    }
}
