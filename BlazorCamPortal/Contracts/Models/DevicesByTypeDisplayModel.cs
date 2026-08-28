namespace CamPortal.Contracts.Models
{
    public class DevicesByTypeDisplayModel
    {
        public Dictionary<DeviceTypeDisplayModel, List<CameraDisplayModel>> Cameras { get; set; } = new Dictionary<DeviceTypeDisplayModel, List<CameraDisplayModel>>();

        public Dictionary<DeviceTypeDisplayModel, List<SmokeAlarmDisplayModel>> SmokeAlarms { get; set; } = new Dictionary<DeviceTypeDisplayModel, List<SmokeAlarmDisplayModel>>();
    }
}
