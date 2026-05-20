using CamPortal.Contracts.Dtos.DeviceTypeDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface IDeviceTypeRepository
    {
        Task<Guid> CreateTypeAsync(CreateDeviceTypeDto dto);

        Task<bool> DeleteTypeAsync(Guid typeId);

        Task<List<DeviceTypeDto>> GetAllTypesAsync();

        Task<DeviceTypeDto?> GetByIdAsync(Guid typeId);

        Task<bool> DoesExistByNameAsync(string name);

        Task<DeviceTypeCategories> GetDeviceCategoryAsync(Guid typeId);

        Task<List<DeviceTypeDto>> GetAllTypesByNameAsync(string name);
    }
}
