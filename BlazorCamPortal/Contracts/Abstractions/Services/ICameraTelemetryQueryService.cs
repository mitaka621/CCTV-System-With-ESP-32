using CamPortal.Contracts.Dtos.TelemetryDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraTelemetryQueryService
    {
        Task<CameraTelemetryAveragesDto> GetLastTwoHoursAveragesAsync(Guid cameraId);
    }
}
