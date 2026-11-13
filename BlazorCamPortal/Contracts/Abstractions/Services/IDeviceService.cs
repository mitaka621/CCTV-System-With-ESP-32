using Homewatch.Contracts.Abstractions.UnitOfWork;
using Homewatch.Contracts.Dtos.CameraDtos;
using Homewatch.Contracts.Dtos.DeviceDtos;
using Homewatch.Contracts.Enums;
using Homewatch.Contracts.Models;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IDeviceService
    {
        Task<Guid> CreateDeviceAsync(CreateDeviceDto dto, IUnitOfWork? uow = null);

        Task<bool> UpdateDeviceAsync(UpdateDeviceDto dto, IUnitOfWork? uow = null);

        Task<DevicesByTypeDisplayModel> GetAllDevicesByTypeAsync(DeviceFilterModel filterModel);

        Task<List<CameraDisplayModel>> GetAllCamerasAsync(params List<Guid> cameraIds);

        Task ChangeStatusAndInvalidateCameraAsync(Guid cameraId, DevicePairStatus newStatus);

        Task<List<NameAndIdWithStatusModel>> GetAllCameraNameAndIdAsync();

        Task<int> GetTotalCamerasAsync(params List<DevicePairStatus> status);

        Task<string?> GetDeviceNameAsync(Guid deviceId);

        Task<bool> DeleteDeviceAsync(Guid deviceId);

        Task<DeviceTypeCategories> GetDeviceCategoryAsync(Guid deviceId);
    }
}
