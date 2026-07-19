using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.TelemetryDtos;

namespace CamPortal.Core.Services.Telemetry
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
