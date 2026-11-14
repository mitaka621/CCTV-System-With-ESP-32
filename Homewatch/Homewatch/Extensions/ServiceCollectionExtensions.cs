using Homewatch.Auth;
using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Abstractions.UnitOfWork;
using Homewatch.Contracts.Constants;
using Homewatch.Core.BackgroundServices;
using Homewatch.Core.LoggerProviders.DatabaseLogger;
using Homewatch.Core.Services.Devices;
using Homewatch.Core.Services.DeviceSessionHandlers;
using Homewatch.Core.Services.Provisioning;
using Homewatch.Core.Services.RemoteStorage;
using Homewatch.Core.Services.SecureStreaming;
using Homewatch.Core.Services.Security;
using Homewatch.Core.Services.SystemServices;
using Homewatch.Core.Services.Telemetry;
using Homewatch.Core.Services.Users;
using Homewatch.Core.Services.Video;
using Homewatch.Core.Utilities;
using Homewatch.Infrastructure.Repositories;
using Homewatch.Infrastructure.Repositories.PerDeviceRepository;
using Homewatch.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;
using MudExtensions.Services;
using System.Net;
using System.Threading.RateLimiting;

namespace Homewatch.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddMudServices(config =>
            {
                config.SnackbarConfiguration.PreventDuplicates = false;
                config.SnackbarConfiguration.ShowTransitionDuration = 200;
                config.SnackbarConfiguration.HideTransitionDuration = 200;
            });
            services.AddMudExtensions();

            services.AddScoped<IUserTimeZoneService, UserTimeZoneService>();

            services.AddSingleton<HttpClient>();
            services.AddSingleton<IStorageLocationService, StorageLocationService>();
            services.AddSingleton<IVideoReplayService, VideoReplayService>();
            services.AddSingleton<IVideoChunkRepository, VideoChunkRepository>();
            services.AddSingleton<IDeviceTypeService, DeviceTypeService>();
            services.AddSingleton<IDeviceTypeRepository, DeviceTypeRepository>();
            services.AddSingleton<IDevicePreProvisionService, DevicePreProvisionService>();
            services.AddSingleton<IDeviceService, DeviceService>();
            services.AddSingleton<IDeviceRepository, DeviceRepository>();
            services.AddSingleton<ICameraConfigurationRepository, CameraConfigurationRepository>();
            services.AddSingleton<ICameraConfigurationService, CameraConfigurationService>();
            services.AddSingleton<IUserCameraLayoutRepository, UserCameraLayoutRepository>();
            services.AddSingleton<IUserCameraLayoutService, UserCameraLayoutService>();
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
            services.AddSingleton<IUserSettingsRepository, UserSettingsRepository>();
            services.AddSingleton<IUserSettingsService, UserSettingsService>();
            services.AddSingleton<IExportedVideoRepository, ExportedVideoRepository>();
            services.AddSingleton<ISystemSettingsRepository, SystemSettingsRepository>();
            services.AddSingleton<ISystemSettingsService, SystemSettingsService>();
            services.AddSingleton<IVideoExportJobQueue, VideoExportJobQueue>();
            services.AddSingleton<IVideoExportNotifier, VideoExportNotifier>();
            services.AddSingleton<IVideoExportService, VideoExportService>();
            services.AddSingleton<ISmokeAlarmDetectorConfigurationRepository, SmokeAlarmDetectorConfigurationRepository>();
            services.AddSingleton<ISmokeAlarmDetectorManagerService, SmokeAlarmDetectorManagerService>();
            services.AddSingleton<ISmokeAlarmTelemetryRepository, SmokeAlarmTelemetryRepository>();
            services.AddSingleton<ISmokeAlarmDetectorConfigurationService, SmokeAlarmDetectorConfigurationService>();
            services.AddSingleton<ICameraRepository, CameraRepository>();
            services.AddSingleton<ISmokeAlarmRepository, SmokeAlarmRepository>();
            services.AddSingleton<ISecureHandshake, SecureHandshake>();
            services.AddSingleton<IRemoteStorageTargetRepository, RemoteStorageTargetRepository>();
            services.AddSingleton<ISmbConnectionService, SmbConnectionService>();
            services.AddSingleton<ISmbReadSessionManager, SmbReadSessionManager>();
            services.AddSingleton<IRemoteStorageService, RemoteStorageTargetService>();
            services.AddSingleton<IVideoChunkReplicationRepository, VideoChunkReplicationRepository>();
            services.AddSingleton<IUserNotificationRepository, NotificationUserRepository>();
            services.AddSingleton<NotificationsService>();
            services.AddSingleton<INotificationsService>(sp => sp.GetRequiredService<NotificationsService>());
            services.AddSingleton<INotificationsNotifier>(sp => sp.GetRequiredService<NotificationsService>());
            services.AddSingleton<INotificationRepository, NotificationRepository>();
            services.AddSingleton<IEmailNotificationSettingsRepository, EmailNotificationSettingsRepository>();
            services.AddSingleton<IEmailNotificationService, GmailNotificationService>();

            services.AddSingleton<VideoExportEncoderService>();
            services.AddSingleton<IVideoExportCanceller>(sp => sp.GetRequiredService<VideoExportEncoderService>());
            services.AddHostedService(sp => sp.GetRequiredService<VideoExportEncoderService>());

            services.AddSingleton<CameraSessionHandler>();
            services.AddSingleton<SmokeAlarmSessionHandler>();
            services.AddSingleton<IDeviceSessionHandler>(sp => sp.GetRequiredService<CameraSessionHandler>());
            services.AddSingleton<IDeviceSessionHandler>(sp => sp.GetRequiredService<SmokeAlarmSessionHandler>());
            services.AddSingleton<ICameraCommandDispatcher>(sp => sp.GetRequiredService<CameraSessionHandler>());
            services.AddSingleton<ISmokeAlarmCommandDispatcher>(sp => sp.GetRequiredService<SmokeAlarmSessionHandler>());

            services.AddSingleton<CameraSecurityCoordinator>();
            services.AddSingleton<ICameraSecurityCoordinator>(sp => sp.GetRequiredService<CameraSecurityCoordinator>());
            services.AddSingleton<ICameraSecurityService>(sp => sp.GetRequiredService<CameraSecurityCoordinator>());
            services.AddSingleton<ICameraLiveTelemetry>(sp => sp.GetRequiredService<CameraSecurityCoordinator>());
            services.AddSingleton<ICameraStatusNotifier>(sp => sp.GetRequiredService<CameraSecurityCoordinator>());

            services.AddSingleton<ICameraTelemetryRepository, CameraTelemetryRepository>();
            services.AddSingleton<ICameraTelemetryQueryService, CameraTelemetryQueryService>();

            services.AddSingleton<CameraTelemetryQueue>();

            services.AddHostedService<SecureTcpServer>();
            services.AddHostedService<VideoEncoderService>();
            services.AddHostedService<RawFrameProcessorService>();

            services.AddHostedService<BackgroundCleanupService>();
            services.AddHostedService<CameraTelemetryWriterService>();

            services.AddSingleton<RemoteStorageReplicationService>();
            services.AddSingleton<IChunkReplicationQueue>(sp => sp.GetRequiredService<RemoteStorageReplicationService>());
            services.AddHostedService(sp => sp.GetRequiredService<RemoteStorageReplicationService>());

            return services;
        }

        public static IServiceCollection AddDataProtectionKeyStorage(this IServiceCollection services, IConfiguration configuration)
        {
            var configuredRoot = configuration.GetSection("ServerStorage")["RootPath"] ?? string.Empty;

            var keysFolder = configuration.GetSection("ServerStorage")["DataProtectionKeysFolder"]
                ?? throw new ArgumentNullException("DataProtectionKeysFolder not configured");

            var storageRoot = string.IsNullOrWhiteSpace(configuredRoot)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(configuredRoot, Directory.GetCurrentDirectory());

            var dataProtectionBuilder = services.AddDataProtection()
                .SetApplicationName("Homewatch")
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(storageRoot, keysFolder)));

            if (OperatingSystem.IsWindows())
            {
                dataProtectionBuilder.ProtectKeysWithDpapi();
            }

            return services;
        }

        public static IServiceCollection AddForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;

                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();

                var knownProxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>();
                if (knownProxies is not null)
                {
                    foreach (var proxy in knownProxies)
                    {
                        if (IPAddress.TryParse(proxy, out var proxyAddress))
                        {
                            options.KnownProxies.Add(proxyAddress);
                        }
                    }
                }
            });

            return services;
        }

        public static IServiceCollection AddRateLimiterPolicy(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                   RateLimitPartition.GetTokenBucketLimiter(
                       partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                       factory: _ => new TokenBucketRateLimiterOptions
                       {
                           TokenLimit = 200,
                           TokensPerPeriod = 5,
                           ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                           QueueLimit = 0,
                           AutoReplenishment = true
                       }));

                options.AddPolicy("preprovision-per-ip", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
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
                            Window = TimeSpan.FromDays(1),
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
                    options.Cookie.Name = "Homewatch.Auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.LoginPath = AppRoutes.Login;
                    options.LogoutPath = "/logout";
                    options.AccessDeniedPath = AppRoutes.AccessDenied;
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
                    options.Cookie.Name = "Homewatch.PwdChangePending";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
                    options.SlidingExpiration = false;
                    options.LoginPath = AppRoutes.Login;
                    options.AccessDeniedPath = AppRoutes.AccessDenied;
                });

            services.AddAuthorization();

            services.AddCascadingAuthenticationState();

            services.AddScoped<AuthenticationStateProvider, RevalidatingAuthStateProvider>();

            services.AddScoped<IAdminAuthorizationGuard, AdminAuthorizationGuard>();

            return services;
        }

        public static ILoggingBuilder AddDatabaseLogging(this ILoggingBuilder logging)
        {
            logging.Services.TryAddSingleton<DatabaseLogQueue>();
            logging.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, DatabaseLoggerProvider>());
            logging.Services.AddHostedService<LoggerDatabaseWriterService>();
            logging.AddFilter<DatabaseLoggerProvider>(level => level >= LogLevel.Information);

            return logging;
        }
    }
}
