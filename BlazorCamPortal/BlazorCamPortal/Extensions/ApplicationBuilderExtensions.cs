using BlazorCamPortal.Contracts.Abstractions.Services;
using BlazorCamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorCamPortal.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder ApplyMigrations(this IApplicationBuilder app, IServiceProvider serviceProvider)
        {
            var dbFactory = serviceProvider.CreateScope()
                .ServiceProvider
                .GetRequiredService<IDbContextFactory<CamPortalDBContext>>();

            using var db = dbFactory.CreateDbContext();

            db.Database.Migrate();

            return app;
        }

        public static async Task<IApplicationBuilder> InitializeCameraFramesManagerServiceAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();

            var service = scope.ServiceProvider.GetRequiredService<ICameraFramesManagerService>();

            var cameraService = scope.ServiceProvider.GetRequiredService<ICameraService>();

            await service.InitializeAsync(cameraService);

            return app;
        }
    }
}
