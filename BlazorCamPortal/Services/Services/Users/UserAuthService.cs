using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.UserDtos;
using CamPortal.Contracts.Models;
using CamPortal.Core.Utilities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace CamPortal.Core.Services.Users
{
    public class UserAuthService : IUserAuthService
    {
        private const int _workFactor = 12;

        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserAuthService> _logger;

        public UserAuthService(IUserRepository userRepository, ILogger<UserAuthService> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<LoginResultDto> LogInAsync(string username, string password)
        {
            var user = await _userRepository.GetUserByUsernameAsync(username);

            if (user == null)
            {
                _logger.LogInformation("Login failed: username {UserName} does not exist.", username);
                return new LoginResultDto { Succeeded = false };
            }

            if (!VerifyPasswordWithHash(password, user.Password))
            {
                _logger.LogInformation("Login failed: wrong password for {UserName}.", username);
                return new LoginResultDto { Succeeded = false };
            }

            if (user.IsFirstTimeSetup)
            {
                return new LoginResultDto
                {
                    Succeeded = true,
                    IsFirstTimeSetup = true,
                    UserId = user.Id
                };
            }

            return new LoginResultDto
            {
                Succeeded = true,
                IsFirstTimeSetup = false,
                UserId = user.Id,
                Principal = BuildPrincipal(user)
            };
        }

        public async Task<ClaimsPrincipal?> BuildPrincipalAsync(Guid userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);

            return user == null ? null : BuildPrincipal(user);
        }

        public async Task<Guid?> CreateUserAsync(CreateUserModel model)
        {
            if (await _userRepository.DoesUserExistAsync(model.UserName, model.Email))
            {
                _logger.LogInformation("CreateUser failed: user {UserName} / {Email} already exists.", model.UserName, model.Email);
                return null;
            }

            var dto = new CreateUserDto
            {
                UserName = model.UserName,
                Email = model.Email,
                Password = HashPassword(model.Password),
                IsFirstTimeSetup = true,
                SecurityStamp = Guid.NewGuid(),
                RoleIds = model.RoleIds
            };

            try
            {
                return await _userRepository.CreateUserAsync(dto);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "CreateUser failed for {UserName}.", model.UserName);
                return null;
            }
        }

        public async Task ForceLogoutAsync(Guid userId)
        {
            var newSecurityStamp = Guid.NewGuid();

            await _userRepository.SetSecurityStampAsync(userId, newSecurityStamp);
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, string newPassword)
        {
            var newHash = HashPassword(newPassword);

            return await _userRepository.UpdatePasswordAsync(userId, newHash, clearFirstTimeSetup: true);
        }

        public async Task<bool> VerifyPasswordAsync(Guid userId, string? password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            var user = await _userRepository.GetUserByIdAsync(userId);

            if (user == null)
            {
                return false;
            }

            return VerifyPasswordWithHash(password, user.Password);
        }

        private static ClaimsPrincipal BuildPrincipal(UserDto user)
        {
            var claims = new List<Claim>
            {
                new(CustomClaimTypes.Id, user.Id.ToString()),
                new(CustomClaimTypes.UserName, user.UserName),
                new(CustomClaimTypes.SecurityStamp, user.SecurityStamp.ToString("N")),
            };

            claims.AddRange(user.Roles.Select(ur => new Claim(CustomClaimTypes.Role, ur.Name)));

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme,
                CustomClaimTypes.UserName,
                CustomClaimTypes.Role);

            return new ClaimsPrincipal(identity);
        }

        private static string HashPassword(string password)
            => BCrypt.Net.BCrypt.HashPassword(password, _workFactor);

        private static bool VerifyPasswordWithHash(string password, string hash)
            => BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
