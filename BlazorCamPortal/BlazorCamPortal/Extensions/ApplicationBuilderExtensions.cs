using BlazorCamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorCamPortal.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder ApplyMigrations(this IApplicationBuilder app, IServiceProvider serviceProvider)
        {
            var db = serviceProvider.CreateScope()
                .ServiceProvider
                .GetRequiredService<CamPortalDBContext>();

            db.Database.Migrate();

            return app;
        }
    }
}
