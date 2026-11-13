using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Dtos.TelemetryDtos;

namespace Homewatch.Core.Services.Telemetry
{
    public class CameraTelemetryQueryService : ICameraTelemetryQueryService
    {
        private readonly ICameraTelemetryRepository _cameraTelemetryRepository;

        public CameraTelemetryQueryService(ICameraTelemetryRepository cameraTelemetryRepository)
        {
            _cameraTelemetryRepository = cameraTelemetryRepository;
        }

        public Task<CameraTelemetryAveragesDto> GetLastTwoHoursAveragesAsync(Guid cameraId)
        {
            return _cameraTelemetryRepository.GetAveragesAsync(cameraId, DateTime.UtcNow.AddHours(-2));
        }
    }
}
