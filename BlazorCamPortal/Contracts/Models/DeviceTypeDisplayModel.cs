using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Models
{
    public class DeviceTypeDisplayModel
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public required string IconName { get; set; }

        public required DateTime IconUpdatedAt { get; set; }

        public DeviceTypeCategories DeviceVariant { get; set; }

        public string IconUrl { get; set; } = string.Empty;
    }
}
