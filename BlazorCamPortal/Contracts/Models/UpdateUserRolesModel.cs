using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Models
{
    public class UpdateUserRolesModel
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one role must be selected.")]
        public List<Guid> RoleIds { get; set; } = new();
    }
}
