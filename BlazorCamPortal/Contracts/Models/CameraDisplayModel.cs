using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Models
{
    public class CameraDisplayModel : DeviceDisplayModel
    {
        public CameraSensorResolutions? SensorResolution { get; set; }

        public CameraAspectRatios? AspectRatio { get; set; }
    }
}
