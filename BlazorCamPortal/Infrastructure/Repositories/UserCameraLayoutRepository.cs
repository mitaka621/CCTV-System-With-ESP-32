using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Dtos.UserCameraLayoutDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Infrastructure.Data;
using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;

namespace CamPortal.Infrastructure.Repositories
{
    public class UserCameraLayoutRepository : IUserCameraLayoutRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly IMapper _mapper;
        private readonly ILogger<UserCameraLayoutRepository> _logger;

        public UserCameraLayoutRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper, ILogger<UserCameraLayoutRepository> logger)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<Queue<UserCameraLayoutItemDto>> GetLayoutForUserAsync(Guid userId)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var layoutItems = await dbContext.UserCameraLayouts
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Camera.PairStatus == DevicePairStatus.Paired)
                .Select(x => new UserCameraLayoutItemDto()
                {
                    LayoutType = x.LayoutType,
                    X = x.X,
                    Y = x.Y,
                    CameraInfo = new CameraInfoWithConfigurationDto()
                    {
                        Id = x.Camera.Id,
                        Name = x.Camera.Name,
                        PairStatus = x.Camera.PairStatus,
                        CreatedAt = x.Camera.CreatedAt,
                        Fingerprint = x.Camera.Fingerprint,
                        Ipv4Address = x.Camera.Ipv4Address,
                        PublicKey = x.Camera.PublicKey,
                        UpdatedAt = x.Camera.UpdatedAt,
                        Configuration = new()
                        {
                            Brightness = x.Camera.CameraConfiguration!.Brightness,
                            Contrast = x.Camera.CameraConfiguration.Contrast,
                            FlipMode = x.Camera.CameraConfiguration.FlipMode,
                            CameraAspectRatio = x.Camera.CameraConfiguration.CameraAspectRatio,
                            FrameRotation = x.Camera.CameraConfiguration.FrameRotation,
                            SharpenFactor = x.Camera.CameraConfiguration.SharpenFactor,
                            ZoomFactor = x.Camera.CameraConfiguration.ZoomFactor,
                            ZoomStartX = x.Camera.CameraConfiguration.ZoomStartX,
                            ZoomStartY = x.Camera.CameraConfiguration.ZoomStartY,
                            ResolutionHeight = x.Camera.CameraConfiguration.ResolutionHeight,
                            ResolutionWidth = x.Camera.CameraConfiguration.ResolutionWidth
                        }
                    }
                })
                .OrderBy(x => x.Y)
                .ThenBy(x => x.X)
                .ToListAsync();

            return new Queue<UserCameraLayoutItemDto>(layoutItems);
        }

        public async Task SaveLayoutForUserAsync(Guid userId, List<UserCameraLayoutItemDto> items)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            if (items.Count == 0)
            {
                return;
            }

            await dbContext.UserCameraLayouts
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync();

            var entities = items.Select(x => new UserCameraPositionLayout()
            {
                CameraId = x.CameraInfo!.Id,
                UserId = userId,
                X = x.X,
                Y = x.Y,
                LayoutType = x.LayoutType
            });

            await dbContext.UserCameraLayouts.AddRangeAsync(entities);
            await dbContext.SaveChangesAsync();
        }

        public async Task<bool> SwapCameraLayoutsAsync(Guid userId, UserCameraLayoutItemDto initialPosition, UserCameraLayoutItemDto targetPosition)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            if (initialPosition.CameraInfo == null)
            {
                return false;
            }

            var initialEntity = await dbContext.UserCameraLayouts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CameraId == initialPosition.CameraInfo.Id);

            if (initialEntity == null)
            {
                _logger.LogError("Camera layout entity not found for user {UserId} and camera {CameraId}", userId, initialPosition.CameraInfo.Id);
                return false;
            }


            if (targetPosition.CameraInfo == null)
            {
                initialEntity.X = targetPosition.X;
                initialEntity.Y = targetPosition.Y;

                await dbContext.SaveChangesAsync();

                return true;
            }

            var targetEntity = await dbContext.UserCameraLayouts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CameraId == targetPosition.CameraInfo.Id);

            if (targetEntity == null)
            {
                _logger.LogError("Camera layout entity not found for user {UserId} and camera {CameraId}", userId, targetPosition.CameraInfo.Id);
                return false;
            }

            var tempX = initialEntity.X;
            var tempY = initialEntity.Y;

            initialEntity.X = targetEntity.X;
            initialEntity.Y = targetEntity.Y;

            targetEntity.X = tempX;
            targetEntity.Y = tempY;

            await dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteExisitingLayoutsForCameraAsync(Guid cameraId)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var result = await dbContext.UserCameraLayouts
                .Where(x => x.CameraId == cameraId)
                .ExecuteDeleteAsync();

            return result > 0;
        }

        public async Task<bool> DeleteAllLayoutsForUserAsync(Guid userId)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var result = await dbContext.UserCameraLayouts
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync();

            return result >= 0;
        }
    }
}
