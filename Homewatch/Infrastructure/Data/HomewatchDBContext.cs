using Homewatch.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Homewatch.Infrastructure.Data
{
    public class HomewatchDBContext : DbContext
    {
        public HomewatchDBContext(DbContextOptions<HomewatchDBContext> options) : base(options) { }

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

        public DbSet<CameraTelemetry> CameraTelemetry { get; set; }

        public DbSet<SmokeAlarmTelemetry> SmokeAlarmTelemetry { get; set; }

        public DbSet<SmokeAlarmDetectorConfiguration> SmokeAlarmDetectorConfigurations { get; set; }

        public DbSet<RemoteStorageTarget> RemoteStorageTargets { get; set; }

        public DbSet<VideoChunkReplication> VideoChunkReplications { get; set; }

        public DbSet<Notification> Notifications { get; set; }

        public DbSet<UserNotification> NotificationsUsers { get; set; }

        public DbSet<EmailNotificationSettings> EmailNotificationSettings { get; set; }


        public static readonly Guid SystemSettingsId = Guid.Parse("8f4d2a1c-0b6e-4c3a-9d57-1f2e3a4b5c6d");

        public static readonly Guid EmailNotificationSettingsId = Guid.Parse("3c9e7b2a-5d41-4f86-a0e3-6b8d2f1c4a97");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SystemSettings>().HasData(
                new SystemSettings
                {
                    Id = SystemSettingsId,
                    EncodedVideoRetention = Contracts.Enums.RetentionPeriod.OneMonth,
                    CameraChunkRetention = Contracts.Enums.RetentionPeriod.OneWeek,
                    DismissedNotificationRetention = Contracts.Enums.RetentionPeriod.OneYear
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
