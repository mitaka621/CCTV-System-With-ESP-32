using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.SystemSettingsDtos
{
    public class SystemSettingsDto
    {
        public RetentionPeriod EncodedVideoRetention { get; set; }

        public RetentionPeriod CameraChunkRetention { get; set; }
    }
}
