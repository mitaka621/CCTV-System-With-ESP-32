using BlazorCamPortal.Contracts.Dtos;
using BlazorCamPortal.Contracts.Enums;

namespace BlazorCamPortal.Contracts.Abstractions.Repositories
{
    public interface ICameraRepository
    {
        Task<Guid> CreateCameraAsync(CreateCameraDto dto);

        Task<bool> DeleteCameraAsync(Guid cameraId);

        Task<bool> SetCameraNameAsync(Guid cameraId, string name);

        Task<PairStatus?> GetCameraStatusAsync(Guid cameraId);

        Task<bool> SetCameraStatusAsync(Guid cameraId, PairStatus newStatus);

        Task<bool> SetCameraStatusAsync(string camerIpv4, PairStatus newStatus);

        Task<CameraDto?> GetCameraByIdAsync(Guid cameraId);

        Task<CameraDto?> GetCameraByMacAsync(string mac);

        Task<CameraDto?> GetCameraByIpAsync(string ipv4);

        Task<bool> DoesCameraIpExistAsync(string ipv4Address);

        Task<bool> DoesCameraMacExistAsync(string macAddress);

        Task<bool> UpdateCameraIpAsync(Guid cameraId, string newIpv4);

        Task<bool> DoesCameraExistWithStatusAsync(string ipv4, string mac, PairStatus status);

        Task<List<CameraDto>> GetAllCamerasAsync();

        Task<bool> SetSessionTokenAsync(string ipv4, string mac, string sessionToken);
    }
}
