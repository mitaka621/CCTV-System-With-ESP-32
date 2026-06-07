using CamPortal.Contracts.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CamPortal.Infrastructure.Data.Entities
{
    [Index(nameof(UserId), nameof(CameraId), IsUnique = true)]
    public class UserCameraPositionLayout
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        public Guid CameraId { get; set; }
        [ForeignKey(nameof(CameraId))]
        public Device Camera { get; set; } = null!;

        [Required]
        public int X { get; set; }

        [Required]
        public int Y { get; set; }

        public CameraLayoutType LayoutType { get; set; }
    }
}
