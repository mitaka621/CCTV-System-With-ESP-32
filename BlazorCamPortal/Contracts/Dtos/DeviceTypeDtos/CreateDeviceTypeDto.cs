using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Dtos.DeviceTypeDtos
{
    public class CreateDeviceTypeDto
    {
        public required string Name { get; set; }

        public required string IconName { get; set; }

        public required DateTime IconUpdatedAt { get; set; }

        public DeviceTypeCategories DeviceCategory { get; set; }

        public string? Description { get; set; }
    }
}
