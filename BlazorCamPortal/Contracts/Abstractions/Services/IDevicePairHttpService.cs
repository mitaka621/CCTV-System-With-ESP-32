namespace BlazorCamPortal.Contracts.Abstractions.Services
{
    public interface IDevicePairHttpService
    {
        Task<List<Guid>> SendChallengeToAllDevicesAsync();
    }
}
