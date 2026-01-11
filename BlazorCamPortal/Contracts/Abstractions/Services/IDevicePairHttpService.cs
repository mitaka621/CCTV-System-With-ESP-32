namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IDevicePairHttpService
    {
        Task<List<Guid>> SendChallengeToAllDevicesAsync();
    }
}
