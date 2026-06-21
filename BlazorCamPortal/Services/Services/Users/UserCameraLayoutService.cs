using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.UserCameraLayoutDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CamPortal.Core.Services.Users
{
    public class UserCameraLayoutService : IUserCameraLayoutService
    {
        private readonly IUserCameraLayoutRepository _userCameraLayoutRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILogger<UserCameraLayoutService> _logger;
        private readonly IUserSettingsService _userSettingsService;

        public UserCameraLayoutService(
            IUserCameraLayoutRepository userCameraLayoutRepository,
            IDeviceRepository deviceRepository,
            IConfiguration configuration,
            ILogger<UserCameraLayoutService> logger,
            IUserSettingsService userSettingsService)
        {
            _userCameraLayoutRepository = userCameraLayoutRepository;
            _deviceRepository = deviceRepository;
            _logger = logger;
            _userSettingsService = userSettingsService;
        }

        public async Task<List<UserCameraLayoutItemDto[]>> GetLatestLayoutAsync(Guid userId)
        {
            var camerasPerRow = await _userSettingsService.GetNumberOfCamerasPerRowAsync(userId);
            var cameras = await _deviceRepository.GetAllCamerasWithConfigurationAsync();
            var configuredCameraPositions = await _userCameraLayoutRepository.GetLayoutForUserAsync(userId);

            int numberOfRows = Math.Max(cameras.Count * 2, 2);

            List<UserCameraLayoutItemDto[]> layoutMatrix = new();

            for (int y = 0; y < numberOfRows; y++)
            {
                layoutMatrix.Add(new UserCameraLayoutItemDto[camerasPerRow]);
                for (int x = 0; x < camerasPerRow; x++)
                {
                    layoutMatrix[y][x] = new UserCameraLayoutItemDto { X = x, Y = y };
                }
            }

            var latestRowTouched = 0;

            while (configuredCameraPositions.Count > 0)
            {
                var cameraPosition = configuredCameraPositions.Dequeue();

                if (!CanPlace(layoutMatrix, cameraPosition))
                {
                    _logger.LogWarning("Invalid camera layout configuration, when loading the configuration from the db for user {UserId} at position X:{X} Y:{Y}", userId, cameraPosition.X, cameraPosition.Y);
                    continue;
                }

                if (cameraPosition.LayoutType == CameraLayoutType.Vertical)
                {
                    layoutMatrix[cameraPosition.Y + 1][cameraPosition.X].LayoutType = CameraLayoutType.Reserved;
                }

                layoutMatrix[cameraPosition.Y][cameraPosition.X] = cameraPosition;

                cameras.Remove(cameraPosition.CameraInfo!.Id);

                latestRowTouched = cameraPosition.LayoutType == CameraLayoutType.Vertical ? cameraPosition.Y + 1 : cameraPosition.Y;
            }

            //for new cameras (no configuration saved for them or the existing configuration became invalid) so new layout is created for them
            for (int y = 0; y < numberOfRows && cameras.Count > 0; y++)
            {
                for (int x = 0; x < camerasPerRow && cameras.Count > 0; x++)
                {
                    var newCamera = cameras.First().Value;

                    var newLayout = new UserCameraLayoutItemDto()
                    {
                        CameraInfo = newCamera,
                        X = x,
                        Y = y,
                    };

                    newLayout.LayoutType = CameraAspectRatioResolver.GetLayoutType(newCamera.Configuration);

                    if (!CanPlace(layoutMatrix, newLayout))
                    {
                        _logger.LogWarning("Invalid camera layout configuration, when creating the latest layout for user {UserId} at position X:{X} Y:{Y}", userId, newLayout.X, newLayout.Y);
                        continue;
                    }

                    if (newLayout.LayoutType == CameraLayoutType.Vertical)
                    {
                        layoutMatrix[y + 1][x].LayoutType = CameraLayoutType.Reserved;
                    }

                    layoutMatrix[y][x] = newLayout;

                    cameras.Remove(newCamera.Id);
                }

                if (cameras.Count == 0 && latestRowTouched < y)
                {
                    latestRowTouched = y;
                    break;
                }
            }

            if (latestRowTouched + 3 < layoutMatrix.Count - 1)
            {
                layoutMatrix.RemoveRange(latestRowTouched + 2, layoutMatrix.Count - (latestRowTouched + 3));
            }

            return layoutMatrix;
        }

        public async Task SaveNewLayoutAsync(Guid userId, List<UserCameraLayoutItemDto[]> items)
        {
            var configurationsToSave = new List<UserCameraLayoutItemDto>();

            for (int i = 0; i < items.Count; i++)
            {
                for (int j = 0; j < items[i].Length; j++)
                {
                    if (!IsPlacementValid(items, items[i][j]))
                    {
                        continue;
                    }

                    if (items[i][j].LayoutType == CameraLayoutType.Vertical || items[i][j].LayoutType == CameraLayoutType.Horizontal)
                    {
                        configurationsToSave.Add(items[i][j]);
                    }
                }
            }

            await _userCameraLayoutRepository.DeleteAllLayoutsForUserAsync(userId);

            await _userCameraLayoutRepository.SaveLayoutForUserAsync(userId, configurationsToSave);
        }

        public async Task<List<UserCameraLayoutItemDto[]>> SwapLayoutsAsync(Guid userId, List<UserCameraLayoutItemDto[]> layoutMatrix, CameraGridCellDto initialPosition, CameraGridCellDto targetPosition)
        {
            if (!CanPlace(layoutMatrix, initialPosition, targetPosition))
            {
                _logger.LogWarning("Invalid camera layout placement for user {UserId}", userId);
                return layoutMatrix;
            }

            var result = await _userCameraLayoutRepository
                .SwapCameraLayoutsAsync(userId, layoutMatrix[initialPosition.Y][initialPosition.X], layoutMatrix[targetPosition.Y][targetPosition.X]);

            if (result)
            {
                var initialCell = layoutMatrix[initialPosition.Y][initialPosition.X];
                var targetCell = layoutMatrix[targetPosition.Y][targetPosition.X];

                if (initialCell.LayoutType == CameraLayoutType.Vertical)
                {
                    (layoutMatrix[initialPosition.Y + 1][initialPosition.X], layoutMatrix[targetPosition.Y + 1][targetPosition.X]) =
                     (layoutMatrix[targetPosition.Y + 1][targetPosition.X], layoutMatrix[initialPosition.Y + 1][initialPosition.X]);
                }

                (layoutMatrix[initialPosition.Y][initialPosition.X], layoutMatrix[targetPosition.Y][targetPosition.X]) =
                    (layoutMatrix[targetPosition.Y][targetPosition.X], layoutMatrix[initialPosition.Y][initialPosition.X]);

                UpdateIndexesInsideMatrix(layoutMatrix);
            }

            return layoutMatrix;
        }

        public bool CanPlace(List<UserCameraLayoutItemDto[]> layoutMatrix, CameraGridCellDto initialPosition, CameraGridCellDto targetPosition)
        {
            var initialCell = layoutMatrix[initialPosition.Y][initialPosition.X];
            var targetCell = layoutMatrix[targetPosition.Y][targetPosition.X];

            if ((initialCell.LayoutType != CameraLayoutType.Horizontal && initialCell.LayoutType != CameraLayoutType.Vertical) || (targetCell.LayoutType == CameraLayoutType.Reserved))
            {
                return false;
            }

            //validation for camera going from initial -> target position
            if (!IsPlacementValid(layoutMatrix, new UserCameraLayoutItemDto() { LayoutType = initialCell.LayoutType, X = targetPosition.X, Y = targetPosition.Y }))
            {
                return false;
            }

            //validation for camera going from target -> initia; position
            if (!IsPlacementValid(layoutMatrix, new UserCameraLayoutItemDto() { LayoutType = targetCell.LayoutType, X = initialCell.X, Y = initialCell.Y }))
            {
                return false;
            }

            return true;
        }

        public bool CanPlace(List<UserCameraLayoutItemDto[]> layoutMatrix, UserCameraLayoutItemDto initialPosition)
        {
            if (!IsPlacementValid(layoutMatrix, initialPosition))
            {
                return false;
            }

            var targetCell = layoutMatrix[initialPosition.Y][initialPosition.X];

            return targetCell.LayoutType == CameraLayoutType.Empty;
        }

        public bool IsPlacementValid(List<UserCameraLayoutItemDto[]> layoutMatrix, UserCameraLayoutItemDto newPosition)
        {
            if (layoutMatrix.Count == 0 || newPosition.X >= layoutMatrix[0].Length || newPosition.Y >= layoutMatrix.Count || newPosition.X < 0 || newPosition.Y < 0)
            {
                return false;
            }

            switch (newPosition.LayoutType)
            {
                case CameraLayoutType.Empty:
                    return true;

                // Cannot place reserved cell by itself
                case CameraLayoutType.Reserved:
                    return false;

                case CameraLayoutType.Horizontal:
                    return true;

                case CameraLayoutType.Vertical:
                    // Cannot place vertical cell in the last row
                    if (newPosition.Y == layoutMatrix.Count - 1)
                    {
                        return false;
                    }

                    return (layoutMatrix[newPosition.Y][newPosition.X].LayoutType == CameraLayoutType.Empty && layoutMatrix[newPosition.Y + 1][newPosition.X].LayoutType == CameraLayoutType.Empty)
                        || (layoutMatrix[newPosition.Y][newPosition.X].LayoutType == CameraLayoutType.Vertical && layoutMatrix[newPosition.Y + 1][newPosition.X].LayoutType == CameraLayoutType.Reserved);

                default:
                    return false;
            }
        }

        private void UpdateIndexesInsideMatrix(List<UserCameraLayoutItemDto[]> layoutMatrix)
        {
            for (int y = 0; y < layoutMatrix.Count; y++)
            {
                for (int x = 0; x < layoutMatrix[y].Length; x++)
                {
                    layoutMatrix[y][x].X = x;
                    layoutMatrix[y][x].Y = y;
                }
            }
        }
    }
}
