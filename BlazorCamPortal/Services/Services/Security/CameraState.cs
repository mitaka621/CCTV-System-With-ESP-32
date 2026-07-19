using CamPortal.Contracts.Dtos.TelemetryDtos;

namespace CamPortal.Core.Services.Security
{
    internal sealed class CameraState
    {
        public readonly object Sync = new();

        public bool Connected;
        public bool Armed;
        public bool CaseSensorInstalled = true;
        public double MovementThresholdOffset;
        public double RotationThresholdOffset;
        public bool AlarmActive;
        public bool AwaitingClear;
        public bool Warning;
        public string? WarningReason;
        public DateTime SuppressTamperUntil;
        public DateTime LastNotifyUtc;
        public CameraTelemetrySampleDto? Latest;
    }
}
