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

        public DbSet<User> Users { get; set; }

        public DbSet<UserRole> UsersRoles { get; set; }

        public DbSet<Role> Roles { get; set; }

        public DbSet<LogMessage> LogMessages { get; set; }

        public DbSet<UserCameraPositionLayout> UserCameraLayouts { get; set; }

        public DbSet<UserSettings> UserSettings { get; set; }

        public DbSet<ExportedVideo> ExportedVideos { get; set; }

        public DbSet<SystemSettings> SystemSettings { get; set; }

        public static readonly Guid SystemSettingsId = Guid.Parse("8f4d2a1c-0b6e-4c3a-9d57-1f2e3a4b5c6d");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SystemSettings>().HasData(
                new SystemSettings
                {
                    Id = SystemSettingsId,
                    EncodedVideoRetention = Contracts.Enums.RetentionPeriod.OneMonth,
                    CameraChunkRetention = Contracts.Enums.RetentionPeriod.OneWeek
                });

            modelBuilder.Entity<Role>().HasData(
                new Role
                {
                    Id = Guid.Parse("b7acd488-f9d9-4026-8b59-3da8637b9e90"),
                    Name = "Admin"
                },
                new Role
                {
                    Id = Guid.Parse("2686070a-a062-48c9-aafe-de74e5c65376"),
                    Name = "User"
                },
                new Role
                {
                    Id = Guid.Parse("60ed5d3e-f4d6-456b-8748-47f9bf9c7c53"),
                    Name = "InfoDashboard"
                });

            base.OnModelCreating(modelBuilder);
        }
    }
}
