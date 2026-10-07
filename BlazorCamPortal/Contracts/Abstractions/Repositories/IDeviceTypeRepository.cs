using CamPortal.Contracts.Dtos.DeviceTypeDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface IDeviceTypeRepository
    {
        Task<Guid> CreateTypeAsync(CreateDeviceTypeDto dto);

        Task<bool> UpdateTypeAsync(UpdateDeviceTypeDto dto);

        Task<bool> DeleteTypeAsync(Guid typeId);

        Task<bool> IsTypeInUseAsync(Guid typeId);

        Task<List<DeviceTypeDto>> GetAllTypesAsync();

        Task<DeviceTypeDto?> GetByIdAsync(Guid typeId);

        Task<bool> DoesExistByNameAsync(string name, Guid? excludedTypeId = null);

        Task<DeviceTypeCategories> GetDeviceCategoryAsync(Guid typeId);

        Task<List<DeviceTypeDto>> GetAllTypesByNameAsync(string name);
    }
}
