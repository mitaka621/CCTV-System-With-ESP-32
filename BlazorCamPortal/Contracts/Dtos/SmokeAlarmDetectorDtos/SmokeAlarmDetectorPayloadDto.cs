using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos
{
    public class SmokeAlarmDetectorPayloadDto
    {
        public Guid DeviceId { get; set; }

        public DateTime LoggedTimeUTC { get; set; }

        public int PayloadVersion { get; set; }

        public SmokeAlarmEvent Event { get; set; }

        public double BatterySOCPercent { get; set; }

        public double BatteryVoltage { get; set; }

        public bool IsCharging { get; set; }

        public int BootCount { get; set; }

        public int DetectedAlarmBeepCount { get; set; }

        override public string ToString()
        {
            return $"DeviceId: {DeviceId}, LoggedTimeUTC: {LoggedTimeUTC}, PayloadVersion: {PayloadVersion}, Event: {Event}, BatterySOCPercent: {BatterySOCPercent}, BatteryVoltage: {BatteryVoltage}, IsCharging: {IsCharging}, BootCount: {BootCount}, DetectedAlarmBeepCount: {DetectedAlarmBeepCount}";
        }
    }
}
