using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Models
{
    public class DeviceTypeDisplayModel
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public required string IconName { get; set; }

        public required DateTime IconUpdatedAt { get; set; }

        public DeviceTypeCategories DeviceCategory { get; set; }

        public string GetIconUrl(IDeviceTypeIconStorageService service)
        {
            return service.BuildPublicUrl(IconName, IconUpdatedAt);
        }
    }
}
