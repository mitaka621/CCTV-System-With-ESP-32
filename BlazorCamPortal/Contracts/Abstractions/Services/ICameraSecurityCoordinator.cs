using CamPortal.Contracts.Dtos.TelemetryDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraSecurityCoordinator
    {
        Task OnCameraConnectedAsync(Guid cameraId);

        void OnCameraDisconnected(Guid cameraId);

        void Ingest(CameraTelemetrySampleDto sample);

        bool RequestSensorResolutionChange(Guid cameraId, CameraSensorResolutions sensorResolution);

        Task<CameraSensorResolutions> SyncCameraResolutionAsync(Guid cameraId, CameraSensorResolutions reportedResolution);
    }
}
