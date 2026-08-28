using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Models
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
            return service.BuildPublicUrl(IconName, DateTime.Now);
        }
    }
}
