using CamPortal.Contracts.Dtos.SecurityDtos;
using CamPortal.Contracts.Dtos.TelemetryDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraLiveTelemetry
    {
        CameraTelemetrySampleDto? GetLatest(Guid cameraId);

        CameraLiveStatusDto GetLiveStatus(Guid cameraId);
    }
}
