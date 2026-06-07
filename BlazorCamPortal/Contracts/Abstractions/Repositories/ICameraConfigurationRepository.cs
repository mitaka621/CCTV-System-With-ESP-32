using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface ICameraConfigurationRepository
    {
        Task<Dictionary<Guid, CameraAspectRatios>> GetCameraAspectRatiosAsync(IReadOnlyList<Guid> cameraIds);

        Task AddDefaultCameraConfigurationToDeviceAsync(Guid deviceId, IUnitOfWork? uow = null);

        Task<bool> UpdateDeviceConfigurationAsync(Guid deviceId, CameraStreamingConfigurationDto dto);

        Task<bool> UpdateCameraResolutionAsync(CameraResolutionDto dto);

        Task<CameraStreamingConfigurationDto?> GetCameraConfigurationAsync(Guid deviceId);

        Task<CameraResolutionDto?> GetCameraResolutionAsync(Guid deviceId);
    }
}
