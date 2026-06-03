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

        Task<string?> GenerateSessionTokenForDeviceAsync(string ipv4, string mac);

        Task<List<CameraDisplayModel>> GetAllCamerasAsync();

        Task<List<CameraDisplayModel>> GetAllCamerasAsync(params List<Guid> cameraIds);

        Task<List<CameraDisplayModel>> GetAllCamerasAsync(params DevicePairStatus[] statuses);

        Task<List<DeviceDto>> GetAllActiveCameraIpsAsync();

        Task ChangeStatusAndInvalidateCameraAsync(Guid cameraId, DevicePairStatus newStatus);

        Task<List<NameAndIdWithStatusModel>> GetAllCameraNameAndIdAsync();

        Task<List<PreprovisionDeviceModel>> GetCamerasByIdAsync(List<Guid> cameraIds);

        Task<int> GetTotalCamerasAsync(params List<DevicePairStatus> status);

        Task<string?> GetDeviceNameAsync(Guid deviceId);
    }
}
