namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IPreprovisionNotifier
    {
        event Action<Guid>? SuccessfulVerification;

        void NotifySuccessfulVerification(Guid deviceId);
    }
}
