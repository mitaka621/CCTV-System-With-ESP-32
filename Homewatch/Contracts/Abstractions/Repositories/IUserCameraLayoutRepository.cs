using Homewatch.Contracts.Dtos.UserCameraLayoutDtos;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface IUserCameraLayoutRepository
    {
        Task<Queue<UserCameraLayoutItemDto>> GetLayoutForUserAsync(Guid userId);

        Task SaveLayoutForUserAsync(Guid userId, List<UserCameraLayoutItemDto> items);

        Task<bool> SwapCameraLayoutsAsync(Guid userId, UserCameraLayoutItemDto initialPosition, UserCameraLayoutItemDto targetPosition);

        Task<bool> DeleteExisitingLayoutsForCameraAsync(Guid cameraId);

        Task<bool> DeleteAllLayoutsForUserAsync(Guid userId);
    }
}
