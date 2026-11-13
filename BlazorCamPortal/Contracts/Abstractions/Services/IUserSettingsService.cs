using Homewatch.Contracts.Models;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IUserSettingsService
    {
        Task<bool> SetNumberOfCamerasPerRowAsync(UserSettingsModel model);

        Task<int> GetNumberOfCamerasPerRowAsync(Guid userId);

        int GetDefaultNumberOfCamerasPerRow();

        Task<bool> ResetCameraGridSettingsAsync(Guid userId);
    }
}
