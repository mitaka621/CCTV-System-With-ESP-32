using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Core.BackgroundServices;
using CamPortal.Core.Services;
using CamPortal.Infrastructure.Repositories;
using MudBlazor.Services;

namespace CamPortal.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddMudServices();

            services.AddScoped<ICameraService, CameraService>();
            services.AddScoped<IDevicePairHttpService, DevicePairHttpService>();
            services.AddScoped<HttpClient>();
            services.AddScoped<IVideoReplayService, VideoReplayService>();
            services.AddScoped<IVideoChunkRepository, VideoChunkRepository>();

            services.AddSingleton<IDeviceAuthenticatorService, DeviceAuthenticatorService>();
            services.AddSingleton<IScanCoordinatorService, ScanCoordinatorService>();
            services.AddSingleton<ICameraFramesManagerService, CameraFramesManagerService>();
            services.AddSingleton<IActiveCameraConnections, ActiveCameraConnections>();

            services.AddHostedService<FramesReceiverTcpService>();
            services.AddHostedService<VideoEncoderService>();
            services.AddHostedService<RawFrameProcessorService>();

            return services;
        }

        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<ICameraRepository, CameraRepository>();

            return services;
        }
    }
}
