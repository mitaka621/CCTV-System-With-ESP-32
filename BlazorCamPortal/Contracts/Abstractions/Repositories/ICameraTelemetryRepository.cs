using CamPortal.Contracts.Dtos.TelemetryDtos;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface ICameraTelemetryRepository
    {
        Task<CameraTelemetryAveragesDto> GetAveragesAsync(Guid cameraId, DateTime sinceUtc);
    }
}
