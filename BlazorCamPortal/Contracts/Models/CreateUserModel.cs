using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Models
{
    public class CreateUserModel
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters.")]
        public string UserName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [Required(ErrorMessage = "Email is required.")]
        [StringLength(200, ErrorMessage = "Email must be at most 200 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Password must contain uppercase, lowercase and number.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "At least one role must be selected.")]
        [MinLength(1, ErrorMessage = "At least one role must be selected.")]
        public List<Guid> RoleIds { get; set; } = new();
    }
}
