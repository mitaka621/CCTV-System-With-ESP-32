namespace CamPortal.Contracts.Models
{
    public class DeviceTypePageModel
    {
        public required List<DeviceTypeDisplayModel> Items { get; set; }

        public int TotalCount { get; set; }
    }
}
