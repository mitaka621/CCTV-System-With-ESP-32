using CamPortal.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Infrastructure.Data.Entities
{
    public class DeviceType
    {
        [Key]
        public Guid Id { get; set; }

        [MaxLength(200)]
        public required string Name { get; set; }

        [MaxLength(500)]
        public required string IconName { get; set; }

        public required DateTime IconUpdatedAt { get; set; }

        public DeviceTypeCategories DeviceCategory { get; set; }
    }
}
