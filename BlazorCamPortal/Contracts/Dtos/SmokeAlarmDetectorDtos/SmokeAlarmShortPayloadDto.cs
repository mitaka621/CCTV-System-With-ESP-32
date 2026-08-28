namespace CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos
{
    public class SmokeAlarmShortPayloadDto
    {
        public double BatterySOCPercent { get; set; }

        public double BatteryVoltage { get; set; }

        public int BootCount { get; set; }

        public DateTime LoggedTimeUTC { get; set; }
    }
}
