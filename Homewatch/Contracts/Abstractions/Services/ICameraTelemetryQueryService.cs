using Homewatch.Contracts.Dtos.TelemetryDtos;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface ICameraTelemetryQueryService
    {
        Task<CameraTelemetryAveragesDto> GetLastTwoHoursAveragesAsync(Guid cameraId);
    }
}
