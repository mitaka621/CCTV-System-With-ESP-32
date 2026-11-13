using Homewatch.Contracts.Dtos.CameraConfigurationDtos;
using Homewatch.Contracts.Models;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface ICameraConfigurationService
    {
        Task<bool> UpdateCameraConfigurationAsync(CameraConfigurationModel dto);

        Task<CameraConfigurationModel?> GetCameraConfigurationAsync(Guid cameraId);
    }
}
