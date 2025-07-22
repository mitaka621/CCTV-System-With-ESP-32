using BlazorCamPortal.Contracts.Enums;
using BlazorCamPortal.Contracts.Models;

namespace BlazorCamPortal.Contracts.Abstractions.Services
{
    public interface ICameraService
    {
        Task<Guid> CreateCameraAsync(CreateCameraModel model, PairStatus cameraStatus);

        Task<bool> DoesCameraExistWithStatusAsync(string ipv4, string mac, PairStatus status);

        Task<bool> UpdateCameraStatusAsync(string mac, PairStatus newStatus);

        Task<List<CameraDisplayModel>> GetAllCamerasAsync();

        Task<string?> GenerateSessionTokenForDeviceAsync(string ipv4, string mac);
    }
}
