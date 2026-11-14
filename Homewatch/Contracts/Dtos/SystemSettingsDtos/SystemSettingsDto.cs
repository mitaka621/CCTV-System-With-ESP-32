using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Dtos.SystemSettingsDtos
{
    public class SystemSettingsDto
    {
        public RetentionPeriod EncodedVideoRetention { get; set; }

        public RetentionPeriod CameraChunkRetention { get; set; }

        public int SecurityMinFps { get; set; } = 4;

        public double SecurityMaxTemperatureC { get; set; } = 60;

        public double SecurityMaxHumidityPercent { get; set; } = 80;

        public RetentionPeriod DismissedNotificationRetention { get; set; } = RetentionPeriod.OneYear;
    }
}
