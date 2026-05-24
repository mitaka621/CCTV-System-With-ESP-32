using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Data
{
    public class CamPortalDBContext : DbContext
    {
        public CamPortalDBContext(DbContextOptions<CamPortalDBContext> options) : base(options) { }

        public DbSet<Device> Devices { get; set; }

        public DbSet<VideoChunk> VideoChunks { get; set; }

        public DbSet<DeviceType> DeviceTypes { get; set; }

        public DbSet<PreprovisionAttempt> PreprovisionAttempts { get; set; }

        public DbSet<CameraConfiguration> CameraConfigurations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //DeviceTypes.Add(new DeviceType
            //{
            //    DeviceVariant = DeviceTypeCategories.Camera,
            //    IconUpdatedAt = DateTime.UtcNow,
            //    Id = Guid.Parse("c9b5fb23-c75f-43c7-80d7-05a767d84207"),
            //    IconName = "camera.png",
            //});
            base.OnModelCreating(modelBuilder);
        }
    }
}
