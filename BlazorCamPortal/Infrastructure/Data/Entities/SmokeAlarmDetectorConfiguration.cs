using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CamPortal.Infrastructure.Data.Entities
{
    public class SmokeAlarmDetectorConfiguration
    {

        [Key]
        public Guid DeviceId { get; set; }
        [ForeignKey(nameof(DeviceId))]
        public Device Device { get; set; } = null!;

        public double MinBatterySOCForAlert { get; set; } = 10.0;
    }
}
