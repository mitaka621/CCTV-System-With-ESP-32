namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface IUserSettingsRepository
    {
        Task<int?> GetNumberOfCamerasPerRowForLiveGridAsync(Guid userId);

        Task<bool> SetNumberOfCamerasPerRowForLiveGridAsync(Guid userId, int NumberOfCamerasPerRow);
    }
}
