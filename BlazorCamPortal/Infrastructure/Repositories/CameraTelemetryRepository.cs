using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Dtos.TelemetryDtos;
using CamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class CameraTelemetryRepository : ICameraTelemetryRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;

        public CameraTelemetryRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<CameraTelemetryAveragesDto> GetAveragesAsync(Guid cameraId, DateTime sinceUtc)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var query = db.CameraTelemetry
                .AsNoTracking()
                .Where(x => x.CameraId == cameraId && x.TimestampUtc >= sinceUtc);

            var sampleCount = await query.CountAsync();

            if (sampleCount == 0)
            {
                return new CameraTelemetryAveragesDto();
            }

            var aggregate = await query
                .GroupBy(_ => 1)
                .Select(g => new CameraTelemetryAveragesDto
                {
                    SampleCount = g.Count(),
                    AvgFps = g.Average(x => x.Fps),
                    AvgCaptureMs = g.Average(x => x.AvgCaptureMs),
                    AvgEncryptMs = g.Average(x => x.AvgEncryptMs),
                    AvgSendMs = g.Average(x => x.AvgSendMs),
                    AvgFrameKB = g.Average(x => x.AvgFrameKB),
                    AvgBufferReadyPercent = g.Average(x => x.BufferReadyPercent),
                    AvgTemperatureC = g.Average(x => x.TemperatureC),
                    AvgHumidityPercent = g.Average(x => x.HumidityPercent),
                    AvgDewPointC = g.Average(x => x.DewPointC),
                    AvgLightSensorValue = g.Average(x => x.LightSensorValue),
                    TotalFailedSends = g.Max(x => x.FailedSends),
                    TotalCaptureFailures = g.Max(x => x.CaptureFailures)
                })
                .FirstAsync();

            return aggregate;
        }
    }
}
