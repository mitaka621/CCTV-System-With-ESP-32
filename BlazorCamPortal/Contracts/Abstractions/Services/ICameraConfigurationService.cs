using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Models;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraConfigurationService
    {
        Task<bool> UpdateCameraConfigurationAsync(CameraConfigurationModel dto);

        Task<CameraConfigurationModel?> GetCameraConfigurationAsync(Guid cameraId);
    }
}
