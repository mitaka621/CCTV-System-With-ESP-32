using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Models
{
    public class CameraDisplayModel : DeviceDisplayModel
    {
        public int? ResolutionWidth { get; set; }

        public int? ResolutionHeight { get; set; }

        public CameraAspectRatios? AspectRatio { get; set; }
    }
}
