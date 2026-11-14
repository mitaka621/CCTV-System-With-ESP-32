using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homewatch.Infrastructure.Data.Entities
{
    [PrimaryKey(nameof(UserId), nameof(NotificationId))]
    [Index(nameof(NotificationId), nameof(IsDismissed))]
    public class UserNotification
    {
        public Guid UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        public Guid NotificationId { get; set; }
        [ForeignKey(nameof(NotificationId))]
        public Notification Notication { get; set; } = null!;

        public bool IsDismissed { get; set; }
    }
}
