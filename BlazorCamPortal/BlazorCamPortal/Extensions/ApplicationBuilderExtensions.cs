using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Constants;
using CamPortal.Contracts.Models;
using CamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Extensions
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

            var cameraService = scope.ServiceProvider.GetRequiredService<IDeviceService>();

            return app;
        }

        public static async Task<WebApplication> RecoverPendingVideoExportsAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();

            var exportedVideoRepository = scope.ServiceProvider.GetRequiredService<IExportedVideoRepository>();
            var videoExportJobQueue = scope.ServiceProvider.GetRequiredService<IVideoExportJobQueue>();

            var pendingExportIds = await exportedVideoRepository.GetPendingExportIdsAsync();

            foreach (var pendingExportId in pendingExportIds)
            {
                videoExportJobQueue.Enqueue(pendingExportId);
            }

            if (pendingExportIds.Count > 0)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("VideoExportRecovery");
                logger.LogInformation("Re-queued {Count} interrupted video export(s) from a previous session.", pendingExportIds.Count);
            }

            return app;
        }

        public static async Task<IApplicationBuilder> SeedInitialAdminAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();

            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CamPortalDBContext>>();

            await using var db = await dbFactory.CreateDbContextAsync();

            if (await db.Users.AnyAsync())
            {
                return app;
            }

            var initialUserName = app.Configuration["InitialAdmin:UserName"] ?? "admin";
            var initialEmail = app.Configuration["InitialAdmin:Email"] ?? "admin@local";
            var initialPassword = app.Configuration["InitialAdmin:Password"] ?? "Admin123";

            var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == Roles.Admin);

            if (adminRole == null)
            {
                return app;
            }

            var userAuthService = scope.ServiceProvider.GetRequiredService<IUserAuthService>();

            var createdId = await userAuthService.CreateUserAsync(new CreateUserModel
            {
                UserName = initialUserName,
                Email = initialEmail,
                Password = initialPassword,
                RoleIds = new List<Guid> { adminRole.Id }
            });

            if (createdId.HasValue)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder");
                logger.LogInformation("Initial admin user '{UserName}' created. Password change required on first login.", initialUserName);
            }

            return app;
        }
    }
}
