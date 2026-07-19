namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraStatusNotifier
    {
        event Action<Guid>? StatusChanged;

        void NotifyStatusChanged(Guid cameraId);
    }
}
