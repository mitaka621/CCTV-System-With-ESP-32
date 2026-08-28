namespace CamPortal.Contracts.Models
{
    public class SmokeAlarmDisplayModel : DeviceDisplayModel
    {
        public double BatterySOCPercent { get; set; }

        public double BatteryVoltage { get; set; }

        public int BootCount { get; set; }

        public DateTime LatestCommunicationTime { get; set; }
    }
}
