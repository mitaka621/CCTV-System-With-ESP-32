using CamPortal.Contracts.Dtos.TelemetryDtos;
using CamPortal.Contracts.Enums;

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
        public CameraSensorResolutions SensorResolution = CameraSensorResolutions.Resolution2048x1536;
        public bool IsSensorResolutionChangePending;
        public bool AlarmActive;
        public bool AwaitingClear;
        public DateTime AwaitingClearUntil;
        public bool Warning;
        public string? WarningReason;
        public DateTime SuppressTamperUntil;
        public DateTime LastNotifyUtc;
        public CameraTelemetrySampleDto? Latest;
    }
}
