using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.CameraDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.ESPSessionTokenDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface IDeviceRepository
    {
        Task<Guid> CreateDeviceAsync(CreateDeviceDto dto, IUnitOfWork? uow = null);

        Task<bool> DeleteDeviceAsync(Guid deviceId);

        Task<DeviceDto?> GetDeviceByIdAsync(Guid deviceId);

        Task<DevicePairStatus?> GetDeviceStatusAsync(Guid deviceId);

        Task<bool> SetDeviceNameAsync(Guid deviceId, string name);

        Task<bool> SetDeviceStatusAsync(Guid deviceId, DevicePairStatus newStatus, IUnitOfWork? uow = null);

        Task<bool> UpdateDeviceIpAsync(Guid deviceId, string newIpv4);

        Task<List<DeviceDto>> GetAllDevicesAsync(params List<Guid> ids);

        Task<bool> UpdateDeviceAsync(UpdateDeviceDto dto, IUnitOfWork? uow = null);

        Task<List<DeviceDto>> GetAllDevicesWithStatusesAsync(params DevicePairStatus[] withStatuses);

        Task<bool> SetSessionTokenAsync(SetESPSessionTokenDto dto);

        Task<List<NameAndIdWithStatusDto>> GetAllDeviceNameAndIdAsync();

        Task<int> GetTotalDevicesAsync(params DevicePairStatus[] status);

        Task<bool> DoesDeviceExistAsync(Guid id);

        Task<DeviceDto?> GetDeviceByIdWithStatusAsync(Guid deviceId, DevicePairStatus status);
    }
}
