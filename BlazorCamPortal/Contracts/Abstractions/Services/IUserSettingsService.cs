using CamPortal.Contracts.Models;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IUserSettingsService
    {
        Task<bool> SetNumberOfCamerasPerRowAsync(UserSettingsModel model);

        Task<int> GetNumberOfCamerasPerRowAsync(Guid userId);

        int GetDefaultNumberOfCamerasPerRow();

        Task<bool> ResetCameraGridSettingsAsync(Guid userId);
    }
}
