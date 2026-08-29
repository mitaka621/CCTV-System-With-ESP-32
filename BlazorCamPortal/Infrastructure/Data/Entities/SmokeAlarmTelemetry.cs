using CamPortal.Contracts.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CamPortal.Infrastructure.Data.Entities
{
    [Index(nameof(LoggedTimeUTC))]
    public class SmokeAlarmTelemetry
    {
        [Key]
        public Guid Id { get; set; }

        public DateTime LoggedTimeUTC { get; set; }

        public SmokeAlarmEvent Event { get; set; }

        public double BatterySOCPercent { get; set; }

        public double BatteryVoltage { get; set; }

        public double ChargingSenseVolts { get; set; }

        public bool IsCharging { get; set; }

        public int BootCount { get; set; }

        public int DetectedAlarmBeepCount { get; set; }

        public Guid DeviceId { get; set; }
        [ForeignKey(nameof(DeviceId))]
        public Device Device { get; set; } = null!;
    }
}
