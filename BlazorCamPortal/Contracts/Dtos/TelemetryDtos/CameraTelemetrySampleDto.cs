using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.TelemetryDtos
{
    public class CameraTelemetrySampleDto
    {
        public Guid CameraId { get; set; }

        public DateTime TimestampUtc { get; set; }

        public double Fps { get; set; }

        public int AvgCaptureMs { get; set; }

        public int MaxCaptureMs { get; set; }

        public int AvgEncryptMs { get; set; }

        public int MaxEncryptMs { get; set; }

        public int AvgSendMs { get; set; }

        public int MaxSendMs { get; set; }

        public int AvgFrameKB { get; set; }

        public int MaxFrameKB { get; set; }

        public int BufferReadyPercent { get; set; }

        public long FrameCount { get; set; }

        public long FailedSends { get; set; }

        public long CaptureFailures { get; set; }

        public int LightSensorValue { get; set; }

        public bool IsNight { get; set; }

        public bool LightSensorPresent { get; set; }

        public double TemperatureC { get; set; }

        public double HumidityPercent { get; set; }

        public double DewPointC { get; set; }

        public bool TempHumiditySensorPresent { get; set; }

        public bool MotionSensorPresent { get; set; }

        public bool CaseOpen { get; set; }

        public bool MotionActive { get; set; }

        public CameraMotionEvents MotionEvents { get; set; }
    }
}
