using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.DeviceTypeDtos
{
    public class DeviceTypeDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public required string IconName { get; set; }

        public required DateTime IconUpdatedAt { get; set; }

        public DeviceTypeCategories DeviceCategory { get; set; }
    }
}
