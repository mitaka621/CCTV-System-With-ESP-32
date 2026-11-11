using CamPortal.Contracts.Enums;
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

        Task<DeviceTypePageModel> GetDeviceTypesPageAsync(DeviceTypeCategories? category, string? name, int startIndex, int count, CancellationToken cancellationToken);

        Task<Dictionary<DeviceTypeCategories, int>> GetDeviceTypeCountsByCategoryAsync();
    }
}
