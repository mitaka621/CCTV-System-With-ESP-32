using CamPortal.Contracts.Dtos.TelemetryDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraSecurityCoordinator
    {
        Task OnCameraConnectedAsync(Guid cameraId);

        void OnCameraDisconnected(Guid cameraId);

        void Ingest(CameraTelemetrySampleDto sample);
    }
}
