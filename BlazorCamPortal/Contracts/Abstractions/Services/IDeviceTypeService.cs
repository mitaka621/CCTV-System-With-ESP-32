using CamPortal.Contracts.Models;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IDeviceTypeService
    {
        Task<List<DeviceTypeDisplayModel>> GetAllDeviceTypesAsync();

        Task<Guid> CreateDeviceTypeAsync(CreateDeviceTypeModel model, CancellationToken ct);

        Task<bool> DeleteDeviceTypeAsync(Guid deviceTypeId);

        Task<bool> DoesDeviceTypeExistByNameAsync(string name);

        Task<List<DeviceTypeDisplayModel>> GetDevicesByNameAsync(string name);

        Task<bool> DoesDeviceSupportQRCodeHandshakeAsync(Guid deviceId);
    }
}
