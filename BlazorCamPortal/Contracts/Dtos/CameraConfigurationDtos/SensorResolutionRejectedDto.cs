using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.CameraConfigurationDtos
{
    public class SensorResolutionRejectedDto
    {
        public Guid CameraId { get; set; }

        public CameraSensorResolutions RequestedResolution { get; set; }

        public CameraSensorResolutions AppliedResolution { get; set; }
    }
}
