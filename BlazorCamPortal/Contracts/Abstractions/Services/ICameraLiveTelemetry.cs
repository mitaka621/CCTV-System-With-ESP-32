using Homewatch.Contracts.Dtos.SecurityDtos;
using Homewatch.Contracts.Dtos.TelemetryDtos;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface ICameraLiveTelemetry
    {
        CameraTelemetrySampleDto? GetLatest(Guid cameraId);

        CameraLiveStatusDto GetLiveStatus(Guid cameraId);
    }
}
