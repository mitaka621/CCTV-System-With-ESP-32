using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Infrastructure.Data.Entities
{
    [Index(nameof(UserName), IsUnique = true)]
    [Index(nameof(Email), IsUnique = true)]
    [Index(nameof(SecurityStamp), IsUnique = true)]
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public required string UserName { get; set; }

        [Required]
        [MaxLength(200)]
        public required string Email { get; set; }

        [MaxLength(1000)]
        [Required]
        public required string Password { get; set; }

        public bool IsFirstTimeSetup { get; set; }

        [MaxLength(1000)]
        public required Guid SecurityStamp { get; set; }

        public List<UserRole> UserRoles { get; set; } = new();

        public UserSettings UserSettings { get; set; } = null!;

        public List<ExportedVideo> ExportedVideos { get; set; } = new();
    }
}
