using System.ComponentModel.DataAnnotations;

namespace CamPortal.Infrastructure.Data.Entities
{
    public class Role
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public required string Name { get; set; }

        public List<UserRole> RoleUsers { get; set; } = new();
    }
}
