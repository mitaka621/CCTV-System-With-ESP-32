namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IUserTimeZoneService
    {
        Task EnsureInitializedAsync();

        DateTime ToLocal(DateTime utcDateTime);

        DateTime ToUtc(DateTime localDateTime);
    }
}
