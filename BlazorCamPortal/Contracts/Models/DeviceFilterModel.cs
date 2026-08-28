using CamPortal.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Models
{
    public class DeviceFilterModel
    {
        [StringLength(100, ErrorMessage = "Search term cannot exceed 100 characters.")]
        public string SearchTerm { get; set; } = string.Empty;

        public IEnumerable<DeviceTypeCategories> SelectedDeviceCategories { get; set; } = new HashSet<DeviceTypeCategories>();

        public IEnumerable<DevicePairStatus> SelectedDevicePairingStatues { get; set; } = new HashSet<DevicePairStatus>
        {
            DevicePairStatus.PairingPending,
            DevicePairStatus.Paired
        };
    }
}
