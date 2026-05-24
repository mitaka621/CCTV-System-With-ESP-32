using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.CameraConfigurationDtos;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface ICameraConfigurationRepository
    {
        Task AddDefaultCameraConfigurationToDeviceAsync(Guid deviceId, IUnitOfWork? uow = null);

        Task<bool> UpdateDeviceConfigurationAsync(Guid deviceId, CameraStreamingConfigurationDto dto);
    }
}
