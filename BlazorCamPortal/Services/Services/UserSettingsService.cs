using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Models;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;

namespace CamPortal.Core.Services
{
    public class UserSettingsService : IUserSettingsService
    {
        private readonly IUserSettingsRepository _userSettingsRepository;
        private readonly IUserCameraLayoutRepository _userCameraLayoutRepository;
        private const int _defaultCamerasPerRow = 4;

        public UserSettingsService(
            IUserSettingsRepository userSettingsRepository,
            IUserCameraLayoutRepository userCameraLayoutRepository,
            IConfiguration configuration)
        {
            _userSettingsRepository = userSettingsRepository;
            _userCameraLayoutRepository = userCameraLayoutRepository;
        }

        public async Task<bool> SetNumberOfCamerasPerRowAsync(UserSettingsModel model)
        {
            if (!MiscUtilities.ValidateModel(model))
            {
                return false;
            }

            return await _userSettingsRepository.SetNumberOfCamerasPerRowForLiveGridAsync(model.UserId, model.NumberOfCamerasPerRow);
        }

        public async Task<int> GetNumberOfCamerasPerRowAsync(Guid userId)
        {
            return await _userSettingsRepository.GetNumberOfCamerasPerRowForLiveGridAsync(userId) ?? _defaultCamerasPerRow;
        }

        public int GetDefaultNumberOfCamerasPerRow()
        {
            return _defaultCamerasPerRow;
        }

        public async Task<bool> ResetCameraGridSettingsAsync(Guid userId)
        {
            await _userCameraLayoutRepository.DeleteAllLayoutsForUserAsync(userId);

            return await _userSettingsRepository.SetNumberOfCamerasPerRowForLiveGridAsync(userId, _defaultCamerasPerRow);
        }
    }
}
