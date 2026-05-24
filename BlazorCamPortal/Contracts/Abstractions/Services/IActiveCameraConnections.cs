namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IActiveCameraConnections
    {
        CancellationToken Register(Guid cameraId, CancellationToken linkedTo);

        bool TryDisconnect(Guid cameraId);
    }
}
