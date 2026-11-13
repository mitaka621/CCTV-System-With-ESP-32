using Homewatch.Contracts.Dtos.CameraConfigurationDtos;
using Homewatch.Contracts.Dtos.DeviceDtos;
using Homewatch.Contracts.Models;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface ICameraRepository
    {
        Task<Dictionary<Guid, CameraInfoWithConfigurationDto>> GetAllCamerasWithConfigurationAsync();

        Task<List<CameraDto>> GetAllCamerasWithTypeAndConfigurationAsync(DeviceFilterModel filterModel);
    }
}
