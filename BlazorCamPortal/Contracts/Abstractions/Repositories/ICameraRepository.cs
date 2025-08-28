using BlazorCamPortal.Contracts.Dtos.CameraDtos;
using BlazorCamPortal.Contracts.Dtos.ESPSessionTokenDtos;
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

        Task<bool> SetCameraStatusAsync(string cameraIpv4, PairStatus newStatus);

        Task<CameraDto?> GetCameraByIdAsync(Guid cameraId);

        Task<CameraDto?> GetCameraByMacAsync(string mac);

        Task<CameraDto?> GetCameraByIpAsync(string ipv4);

        Task<bool> DoesCameraIpExistAsync(string ipv4Address);

        Task<bool> DoesCameraMacExistAsync(string macAddress);

        Task<bool> UpdateCameraIpAsync(Guid cameraId, string newIpv4);

        Task<bool> DoesCameraExistWithStatusAsync(string ipv4, string mac, PairStatus[] statuses);

        Task<bool> DoesCameraExistWithStatusAsync(string ipv4, PairStatus[] statuses);

        Task<List<CameraDto>> GetAllCamerasAsync(params List<Guid> Ids);

        Task<List<CameraDto>> GetAllCamerasAsync(PairStatus[] statuses);

        Task<bool> SetSessionTokenAsync(SetESPSessionTokenDto dto);

        Task<ESPSessionTokenDto?> GetSessionTokenAsync(string ipv4, string mac);

        Task<List<string>> GetAllPairedCamerasAsync();

        Task<Guid> GetCameraIdAsync(string ipv4, string mac);

        Task<List<NameAndIdWithStatusDto>> GetAllCameraNameAndIdAsync();

        Task<int> GetTotalCamerasAsync(params List<PairStatus> status);
    }
}
