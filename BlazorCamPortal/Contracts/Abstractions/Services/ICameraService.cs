using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Models;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraService
    {
        Task<Guid> CreateCameraAsync(CreateCameraModel model, PairStatus cameraStatus);

        Task<bool> DoesCameraExistWithStatusAsync(string ipv4, string mac, params PairStatus[] statuses);

        Task<bool> DoesCameraExistWithStatusAsync(string ipv4, params PairStatus[] statuses);

        Task<bool> UpdateCameraStatusAsync(string mac, PairStatus newStatus);

        Task<List<CameraDisplayModel>> GetAllCamerasAsync();

        Task<List<CameraDisplayModel>> GetAllCamerasAsync(params List<Guid> Ids);

        Task<List<CameraDisplayModel>> GetAllCamerasAsync(params PairStatus[] statuses);

        Task<string?> GenerateSessionTokenForDeviceAsync(string ipv4, string mac);

        Task<(byte[]? token, bool isExpired)> GetSessionTokenAsByteArrayAsync(string ipv4, string mac);

        Task<List<string>> GetAllActiveCameraIpsAsync();

        Task ChangeCameraStatusAsync(Guid cameraId, PairStatus newStatus);

        Task<Guid> GetCameraIdAsync(string ipv4, string mac);

        Task<List<NameAndIdWithStatusModel>> GetAllCameraNameAndIdAsync();

        Task<List<CreateCameraModel>> GetCamerasByIdAsync(List<Guid> cameraIds);

        Task<int> GetTotalCamerasAsync(params List<PairStatus> status);
    }
}
