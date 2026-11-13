using Homewatch.Contracts.Dtos.TelemetryDtos;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface ICameraTelemetryRepository
    {
        Task<CameraTelemetryAveragesDto> GetAveragesAsync(Guid cameraId, DateTime sinceUtc);
    }
}
