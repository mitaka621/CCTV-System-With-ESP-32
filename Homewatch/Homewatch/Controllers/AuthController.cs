using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Constants;
using Homewatch.Contracts.Models;
using Homewatch.Core.Utilities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Homewatch.Controllers
{
    [Route("api/auth")]
    [EnableRateLimiting("auth-per-ip")]
    public class AuthController : ControllerBase
    {
        private readonly IUserAuthService _userAuthService;

        public AuthController(IUserAuthService userAuthService)
        {
            _userAuthService = userAuthService;
        }

        [HttpPost("login")]
        [Consumes("application/x-www-form-urlencoded")]
        [RequireAntiforgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromForm] LoginModel model, [FromForm] string? returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return LocalRedirect(AppRoutes.LoginWithError("invalid"));
            }

            var result = await _userAuthService.LogInAsync(model.UserName, model.Password);

            if (!result.Succeeded)
            {
                return LocalRedirect(AppRoutes.LoginWithError("credentials"));
            }

            if (result.IsFirstTimeSetup)
            {
                var pendingIdentity = new ClaimsIdentity(new[]
                {
                    new Claim(CustomClaimTypes.Id, result.UserId.ToString())
                }, AuthSchemes.PasswordChangePending);

                await HttpContext.SignInAsync(
                    AuthSchemes.PasswordChangePending,
                    new ClaimsPrincipal(pendingIdentity),
                    new AuthenticationProperties
                    {
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10),
                        IsPersistent = false,
                    });

                return LocalRedirect(AppRoutes.ChangePassword);
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                result.Principal!,
                new AuthenticationProperties { IsPersistent = true });

            return LocalRedirect(ResolveLandingUrl(result.Principal!, returnUrl));
        }

        [HttpPost("logout")]
        [Authorize]
        [RequireAntiforgeryToken]
        public async Task<IActionResult> Logout()
        {
            await Signout();

            return LocalRedirect(AppRoutes.Login);
        }

        [HttpPost("change-password")]
        [Consumes("application/x-www-form-urlencoded")]
        [AllowAnonymous]
        [RequireAntiforgeryToken]
        public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordModel model)
        {
            var passwordChangePendingAuth = await HttpContext.AuthenticateAsync(AuthSchemes.PasswordChangePending);

            var regularAuth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            string? idStr;

            if (regularAuth.Succeeded && regularAuth.Principal != null)
            {
                idStr = regularAuth.Principal.FindFirst(CustomClaimTypes.Id)?.Value;
            }
            else if (passwordChangePendingAuth.Succeeded && passwordChangePendingAuth.Principal != null)
            {
                idStr = passwordChangePendingAuth.Principal.FindFirst(CustomClaimTypes.Id)?.Value;
            }
            else
            {
                return LocalRedirect(AppRoutes.LoginWithError("expired"));
            }

            if (!Guid.TryParse(idStr, out var userId))
            {
                await Signout();

                return LocalRedirect(AppRoutes.LoginWithError("expired"));
            }

            if (!ModelState.IsValid)
            {
                return LocalRedirect(AppRoutes.ChangePasswordWithError("invalid"));
            }

            if (regularAuth.Succeeded && !await _userAuthService.VerifyPasswordAsync(userId, model.OldPassword))
            {
                return LocalRedirect(AppRoutes.ChangePasswordWithError("invalid-old"));
            }

            var changed = await _userAuthService.ChangePasswordAsync(userId, model.NewPassword);

            if (!changed)
            {
                return LocalRedirect(AppRoutes.ChangePasswordWithError("failed"));
            }

            await HttpContext.SignOutAsync(AuthSchemes.PasswordChangePending);

            var principal = await _userAuthService.BuildPrincipalAsync(userId);

            if (principal == null)
            {
                return LocalRedirect(AppRoutes.LoginWithError("failed"));
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            return LocalRedirect(ResolveLandingUrl(principal, null));
        }

        private string ResolveLandingUrl(System.Security.Claims.ClaimsPrincipal principal, string? returnUrl)
        {
            if (IsSafeLocalUrl(returnUrl))
            {
                return returnUrl!;
            }

            if (principal.IsInRole(Roles.Admin) || principal.IsInRole(Roles.User))
            {
                return AppRoutes.Dashboard;
            }

            if (principal.IsInRole(Roles.InfoDashboard))
            {
                return AppRoutes.InfoDashboard;
            }

            return AppRoutes.AccessDenied;
        }

        private bool IsSafeLocalUrl(string? url)
        {
            return !string.IsNullOrEmpty(url)
                && Url.IsLocalUrl(url);
        }

        private async Task Signout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignOutAsync(AuthSchemes.PasswordChangePending);
        }
    }
}
