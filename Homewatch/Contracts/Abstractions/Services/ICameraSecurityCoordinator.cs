using Homewatch.Contracts.Dtos.TelemetryDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Abstractions.Services
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
