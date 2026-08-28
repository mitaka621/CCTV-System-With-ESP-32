using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Models;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface ICameraRepository
    {
        Task<Dictionary<Guid, CameraInfoWithConfigurationDto>> GetAllCamerasWithConfigurationAsync();

        Task<List<CameraDto>> GetAllCamerasWithTypeAndConfigurationAsync(DeviceFilterModel filterModel);
    }
}
