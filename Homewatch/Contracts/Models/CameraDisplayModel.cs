using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Models
{
    public class CameraDisplayModel : DeviceDisplayModel
    {
        public CameraSensorResolutions? SensorResolution { get; set; }

        public CameraAspectRatios? AspectRatio { get; set; }
    }
}
