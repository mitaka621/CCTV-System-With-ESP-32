using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Core.BackgroundServices;
using CamPortal.Core.Services;
using CamPortal.Infrastructure.Repositories;
using CamPortal.Infrastructure.UnitOfWork;
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

            services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();
            services.AddSingleton<IDeviceTypeIconStorageService, DeviceTypeIconStorageService>();
            services.AddSingleton<IDeviceAuthenticatorService, DeviceAuthenticatorService>();
            services.AddSingleton<ICameraFramesManagerService, CameraFramesManagerService>();
            services.AddSingleton<IActiveCameraConnections, ActiveCameraConnections>();
            services.AddSingleton<IPreprovisionAttemptRepository, PreprovisionAttemptRepository>();
            services.AddSingleton<IPreprovisionNotifier, PreprovisionNotifier>();

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
            });

            return services;
        }
    }
}
