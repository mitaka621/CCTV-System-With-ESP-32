namespace CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos
{
    public class SmokeAlarmConfigurationDto
    {
        public Guid DeviceId { get; set; }

        public double MinBatterySOCForAlert { get; set; }

        public double ChargeSenseVoltageThreashold { get; set; }

        public double MaxVoltageOverchargeWarning { get; set; }
    }
}
