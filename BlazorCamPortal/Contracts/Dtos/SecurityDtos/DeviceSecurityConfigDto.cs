namespace CamPortal.Contracts.Dtos.SecurityDtos
{
    public class DeviceSecurityConfigDto
    {
        public bool SecurityArmed { get; set; }

        public bool CaseSensorInstalled { get; set; }

        public double MovementThresholdOffset { get; set; }

        public double RotationThresholdOffset { get; set; }
    }
}
