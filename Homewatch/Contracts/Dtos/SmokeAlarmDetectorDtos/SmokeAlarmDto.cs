using Homewatch.Contracts.Dtos.DeviceDtos;

namespace Homewatch.Contracts.Dtos.SmokeAlarmDetectorDtos
{
    public class SmokeAlarmDto : DeviceDto
    {
        public required SmokeAlarmShortPayloadDto LatestTelemetry { get; set; }
    }
}
