using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace CamPortal.Infrastructure.Data.Entities
{
    [PrimaryKey(nameof(UserId), nameof(RoleId))]
    public class UserRole
    {
        public Guid UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        public Guid RoleId { get; set; }
        [ForeignKey(nameof(RoleId))]
        public Role Role { get; set; } = null!;
    }
}
