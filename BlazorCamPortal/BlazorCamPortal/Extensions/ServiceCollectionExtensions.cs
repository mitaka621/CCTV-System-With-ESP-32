using BlazorCamPortal.Contracts.Abstractions.Repositories;
using BlazorCamPortal.Contracts.Abstractions.Services;
using BlazorCamPortal.Core.Services;
using BlazorCamPortal.Infrastructure.Repositories;

namespace BlazorCamPortal.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddScoped<ICameraService, CameraService>();
            services.AddScoped<IDeviceAuthenticatorService, DeviceAuthenticatorService>();
            services.AddScoped<IDevicePairHttpService, DevicePairHttpService>();
            services.AddScoped<HttpClient>();

            services.AddSingleton<IScanCoordinatorService, ScanCoordinatorService>();

            return services;
        }

        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<ICameraRepository, CameraRepository>();

            return services;
        }
    }
}
