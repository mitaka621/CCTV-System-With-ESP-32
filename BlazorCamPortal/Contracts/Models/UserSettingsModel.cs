using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Models
{
    public class UserSettingsModel
    {
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "Number of cameras per row is required.")]
        [Range(1, 10, ErrorMessage = "Number of cameras per row must be between 1 and 10.")]
        public int NumberOfCamerasPerRow { get; set; }
    }
}
