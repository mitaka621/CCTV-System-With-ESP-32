namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IActiveCameraConnections
    {
        CancellationToken Register(Guid cameraId, CancellationToken linkedTo);

        void Unregister(Guid cameraId);

        bool TryDisconnect(Guid cameraId);
    }
}
