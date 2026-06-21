using CamPortal.Contracts.Dtos.UserCameraLayoutDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IUserCameraLayoutService
    {
        Task<List<UserCameraLayoutItemDto[]>> GetLatestLayoutAsync(Guid userId);

        Task SaveNewLayoutAsync(Guid userId, List<UserCameraLayoutItemDto[]> items);

        Task<List<UserCameraLayoutItemDto[]>> SwapLayoutsAsync(Guid userId, List<UserCameraLayoutItemDto[]> layoutMatrix, CameraGridCellDto initialPosition, CameraGridCellDto targetPosition);

        bool CanPlace(List<UserCameraLayoutItemDto[]> layoutMatrix, CameraGridCellDto initialPosition, CameraGridCellDto targetPosition);

        bool CanPlace(List<UserCameraLayoutItemDto[]> layoutMatrix, UserCameraLayoutItemDto initialPosition);
    }
}
