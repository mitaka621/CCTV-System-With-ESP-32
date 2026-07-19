using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.SecurityDtos
{
    public class CameraSecurityStatusDto
    {
        public Guid CameraId { get; set; }

        public bool Online { get; set; }

        public bool SecurityArmed { get; set; }

        public bool CaseSensorInstalled { get; set; }

        public bool AlarmActive { get; set; }

        public bool CaseOpen { get; set; }

        public bool MotionActive { get; set; }

        public CameraMotionEvents MotionEvents { get; set; }

        public bool TempHumiditySensorPresent { get; set; }

        public bool MotionSensorPresent { get; set; }

        public double TemperatureC { get; set; }

        public double HumidityPercent { get; set; }

        public double DewPointC { get; set; }

        public double MovementThresholdOffset { get; set; }

        public double RotationThresholdOffset { get; set; }

        public bool HasTelemetry { get; set; }
    }
}
