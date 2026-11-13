using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Dtos.CameraConfigurationDtos
{
    public class SensorResolutionRejectedDto
    {
        public Guid CameraId { get; set; }

        public CameraSensorResolutions RequestedResolution { get; set; }

        public CameraSensorResolutions AppliedResolution { get; set; }
    }
}
