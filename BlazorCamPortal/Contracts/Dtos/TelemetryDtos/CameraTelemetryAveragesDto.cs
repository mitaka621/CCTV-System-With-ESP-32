namespace CamPortal.Contracts.Dtos.TelemetryDtos
{
    public class CameraTelemetryAveragesDto
    {
        public int SampleCount { get; set; }

        public double AvgFps { get; set; }

        public double AvgCaptureMs { get; set; }

        public double AvgEncryptMs { get; set; }

        public double AvgSendMs { get; set; }

        public double AvgFrameKB { get; set; }

        public double AvgBufferReadyPercent { get; set; }

        public double AvgTemperatureC { get; set; }

        public double AvgHumidityPercent { get; set; }

        public double AvgDewPointC { get; set; }

        public double AvgLightSensorValue { get; set; }

        public long TotalFailedSends { get; set; }

        public long TotalCaptureFailures { get; set; }
    }
}
