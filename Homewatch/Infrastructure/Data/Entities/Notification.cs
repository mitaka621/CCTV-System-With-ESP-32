using Homewatch.Contracts.Enums;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homewatch.Infrastructure.Data.Entities
{
    [Index(nameof(TopicIdentifier))]
    public class Notification
    {
        [Key]
        public Guid Id { get; set; }

        //hashed string, constructed from the notification name, description and url used for traking duplicate notifications
        [Required]
        [Column(TypeName = "binary(32)")]
        public required byte[] TopicIdentifier { get; set; }

        [Required]
        [StringLength(50)]
        public required string Name { get; set; }

        [Required]
        [StringLength(500)]
        public required string Description { get; set; }

        public Color IconColor { get; set; }

        [Required]
        [StringLength(10000)]
        public string Icon { get; set; } = Icons.Material.Filled.Info;

        [StringLength(500)]
        public string? RelativeRedirectUrl { get; set; }

        [Required]
        public DateTime DateCreatedUTC { get; set; }

        public NotificationType NotificationType { get; set; }

        public List<UserNotification> NotificationUsers { get; set; } = new();
    }
}
