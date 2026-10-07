using CamPortal.Contracts.Models;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IDeviceTypeService
    {
        Task<List<DeviceTypeDisplayModel>> GetAllDeviceTypesAsync();

        Task<Guid> CreateDeviceTypeAsync(CreateDeviceTypeModel model, CancellationToken ct);

        Task<bool> UpdateDeviceTypeAsync(Guid deviceTypeId, CreateDeviceTypeModel model, CancellationToken ct);

        Task<bool> DeleteDeviceTypeAsync(Guid deviceTypeId);

        Task<bool> DoesDeviceTypeExistByNameAsync(string name, Guid? excludedDeviceTypeId = null);

        Task<bool> IsDeviceTypeInUseAsync(Guid deviceTypeId);

        Task<List<DeviceTypeDisplayModel>> GetDevicesByNameAsync(string name);
    }
}
