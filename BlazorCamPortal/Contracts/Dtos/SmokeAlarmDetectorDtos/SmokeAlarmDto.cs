using CamPortal.Contracts.Dtos.DeviceDtos;

namespace CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos
{
    public class SmokeAlarmDto : DeviceDto
    {
        public required SmokeAlarmShortPayloadDto LatestTelemetry { get; set; }
    }
}
