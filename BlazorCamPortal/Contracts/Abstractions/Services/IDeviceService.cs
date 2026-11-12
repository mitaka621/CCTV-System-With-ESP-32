using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.CameraDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Models;

namespace CamPortal.Contracts.Abstractions.Services
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
