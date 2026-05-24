using CamPortal.Contracts.Models;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraConfigurationService
    {
        Task<bool> UpdateCameraConfigurationAsync(Guid deviceId, CameraConfigurationModel dto);
    }
}
