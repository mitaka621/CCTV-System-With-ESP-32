using CamPortal.Auth;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Constants;
using CamPortal.Core.BackgroundServices;
using CamPortal.Core.Services;
using CamPortal.Core.Utilities;
using CamPortal.Infrastructure.Repositories;
using CamPortal.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using System.Threading.RateLimiting;

namespace CamPortal.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddMudServices();

            services.AddScoped<IDeviceService, DeviceService>();
            services.AddScoped<HttpClient>();
            services.AddScoped<IVideoReplayService, VideoReplayService>();
            services.AddScoped<IVideoChunkRepository, VideoChunkRepository>();
            services.AddScoped<IDeviceTypeService, DeviceTypeService>();
            services.AddScoped<IDeviceTypeRepository, DeviceTypeRepository>();
            services.AddScoped<IDevicePreProvisionService, DevicePreProvisionService>();
            services.AddScoped<ICameraConfigurationRepository, CameraConfigurationRepository>();
            services.AddScoped<ICameraConfigurationService, CameraConfigurationService>();

            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<IUserAuthService, UserAuthService>();
            services.AddSingleton<IUserManagementService, UserManagementService>();
            services.AddSingleton<IUserRoleRepository, UserRoleRepository>();
            services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();
            services.AddSingleton<IDeviceTypeIconStorageService, DeviceTypeIconStorageService>();
            services.AddSingleton<IDeviceAuthenticatorService, DeviceAuthenticatorService>();
            services.AddSingleton<ICameraFramesManagerService, CameraFramesManagerService>();
            services.AddSingleton<IActiveCameraConnections, ActiveCameraConnections>();
            services.AddSingleton<IPreprovisionAttemptRepository, PreprovisionAttemptRepository>();
            services.AddSingleton<IPreprovisionNotifier, PreprovisionNotifier>();
            services.AddSingleton<IServerIdentityService, ServerIdentityService>();

            services.AddHostedService<FramesReceiverTcpService>();
            services.AddHostedService<VideoEncoderService>();
            services.AddHostedService<RawFrameProcessorService>();

            return services;
        }

        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<IDeviceRepository, DeviceRepository>();

            return services;
        }

        public static IServiceCollection AddRateLimiterPolicy(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                   RateLimitPartition.GetFixedWindowLimiter(
                       partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                       factory: _ => new FixedWindowRateLimiterOptions
                       {
                           PermitLimit = 100,
                           Window = TimeSpan.FromMinutes(1),
                           QueueLimit = 0,
                           AutoReplenishment = true
                       }));

                options.AddPolicy("preprovision-per-ip", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromHours(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));

                options.AddPolicy("auth-per-ip", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromHours(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });

            return services;
        }

        public static IServiceCollection AddAuth(this IServiceCollection services)
        {
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "CamPortal.Auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.LoginPath = "/login";
                    options.LogoutPath = "/logout";
                    options.AccessDeniedPath = "/access-denied";
                    options.ExpireTimeSpan = TimeSpan.FromDays(7);
                    options.SlidingExpiration = true;
                    options.Events.OnValidatePrincipal = async ctx =>
                    {
                        var idStr = ctx.Principal?.FindFirst(CustomClaimTypes.Id)?.Value;
                        var stampStr = ctx.Principal?.FindFirst(CustomClaimTypes.SecurityStamp)?.Value;
                        if (!Guid.TryParse(idStr, out var userId) ||
                            !Guid.TryParseExact(stampStr, "N", out var cookieStamp))
                        {
                            ctx.RejectPrincipal();
                            await ctx.HttpContext.SignOutAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme);
                            return;
                        }
                        var userRepository = ctx.HttpContext.RequestServices
                            .GetRequiredService<IUserRepository>();

                        var currentStamp = await userRepository.GetSecurityStampAsync(userId);

                        if (currentStamp == Guid.Empty || currentStamp != cookieStamp)
                        {
                            ctx.RejectPrincipal();
                            await ctx.HttpContext.SignOutAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                    };
                })
                .AddCookie(AuthSchemes.PasswordChangePending, options =>
                {
                    options.Cookie.Name = "CamPortal.PwdChangePending";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
                    options.SlidingExpiration = false;
                    options.LoginPath = "/login";
                    options.AccessDeniedPath = "/access-denied";
                });

            services.AddAuthorization();

            services.AddCascadingAuthenticationState();

            services.AddScoped<AuthenticationStateProvider, RevalidatingAuthStateProvider>();

            return services;
        }
    }
}
