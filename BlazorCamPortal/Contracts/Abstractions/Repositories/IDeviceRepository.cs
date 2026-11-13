using Homewatch.Contracts.Abstractions.UnitOfWork;
using Homewatch.Contracts.Dtos.CameraDtos;
using Homewatch.Contracts.Dtos.DeviceDtos;
using Homewatch.Contracts.Dtos.ESPSessionTokenDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface IDeviceRepository
    {
        Task<Guid> CreateDeviceAsync(CreateDeviceDto dto, IUnitOfWork? uow = null);

        Task<bool> DeleteDeviceAsync(Guid deviceId);

        Task<DeviceDto?> GetDeviceByIdAsync(Guid deviceId);

        Task<DevicePairStatus?> GetDeviceStatusAsync(Guid deviceId);

        Task<bool> SetDeviceNameAsync(Guid deviceId, string name, IUnitOfWork? uow = null);

        Task<string?> GetDeviceNameAsync(Guid deviceId);

        Task<bool> SetDeviceStatusAsync(Guid deviceId, DevicePairStatus newStatus, IUnitOfWork? uow = null);

        Task<bool> UpdateDeviceIpAsync(Guid deviceId, string newIpv4, IUnitOfWork? uow = null);

        Task<List<DeviceWithPreprovisionAttemptsDto>> GetAllDevicesAsync(params List<Guid> ids);

        Task<bool> UpdateDeviceAsync(UpdateDeviceDto dto, IUnitOfWork? uow = null);

        Task<List<DeviceWithPreprovisionAttemptsDto>> GetAllDevicesWithStatusesAsync(params DevicePairStatus[] withStatuses);

        Task<bool> SetSessionTokenAsync(SetESPSessionTokenDto dto);

        Task<List<NameAndIdWithStatusDto>> GetAllDeviceNameAndIdAsync(DeviceTypeCategories category);

        Task<int> GetTotalDevicesAsync(params DevicePairStatus[] status);

        Task<bool> DoesDeviceExistAsync(Guid id);

        Task<DeviceWithPreprovisionAttemptsDto?> GetDeviceByIdWithStatusAsync(Guid deviceId, DevicePairStatus status);

        Task<DeviceStreamingHandshakeDto?> GetDeviceForStreamingHandshakeAsync(Guid deviceId);

        Task<int> CountAllDevicesFromCategoryAsync(DeviceTypeCategories category);

        Task<DeviceTypeCategories?> GetDeviceCategoryAsync(Guid deviceId);
    }
}
