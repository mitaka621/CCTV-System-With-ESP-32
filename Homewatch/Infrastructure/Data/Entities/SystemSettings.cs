using Homewatch.Contracts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homewatch.Infrastructure.Data.Entities
{
    public class SystemSettings
    {
        [Key]
        public Guid Id { get; set; }

        public RetentionPeriod EncodedVideoRetention { get; set; }

        public RetentionPeriod CameraChunkRetention { get; set; }

        public int SecurityMinFps { get; set; } = 4;

        public double SecurityMaxTemperatureC { get; set; } = 90;

        public double SecurityMaxHumidityPercent { get; set; } = 70;

        public RetentionPeriod DismissedNotificationRetention { get; set; } = RetentionPeriod.OneYear;
    }
}
