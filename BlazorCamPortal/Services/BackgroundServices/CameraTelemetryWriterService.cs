using CamPortal.Contracts.Dtos.TelemetryDtos;
using CamPortal.Core.Services.Telemetry;
using CamPortal.Infrastructure.Data;
using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CamPortal.Core.BackgroundServices
{
    public class CameraTelemetryWriterService : BackgroundService
    {
        private const int _flushIntervalInSeconds = 60;

        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly CameraTelemetryQueue _queue;
        private readonly ILogger<CameraTelemetryWriterService> _logger;

        public CameraTelemetryWriterService(
            CameraTelemetryQueue queue,
            IDbContextFactory<CamPortalDBContext> dbContextFactory,
            ILogger<CameraTelemetryWriterService> logger)
        {
            _queue = queue;
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                List<CameraTelemetrySampleDto> batch = new();

                var nextFlush = DateTime.UtcNow.AddSeconds(_flushIntervalInSeconds);

                while (nextFlush > DateTime.UtcNow && !stoppingToken.IsCancellationRequested)
                {
                    batch.Add(await _queue.Reader.ReadAsync(stoppingToken));
                }

                await WriteTelemetryToDatabaseAsync(batch);
            }

            _queue.Complete();

            List<CameraTelemetrySampleDto> endBatch = new();

            await foreach (var sample in _queue.Reader.ReadAllAsync())
            {
                endBatch.Add(sample);
            }

            await WriteTelemetryToDatabaseAsync(endBatch);
        }

        private async Task WriteTelemetryToDatabaseAsync(List<CameraTelemetrySampleDto> batch)
        {
            if (batch.Count == 0)
            {
                return;
            }

            var entities = batch.Select(sample => new CameraTelemetry
            {
                Id = Guid.NewGuid(),
                CameraId = sample.CameraId,
                TimestampUtc = sample.TimestampUtc,
                Fps = sample.Fps,
                AvgCaptureMs = sample.AvgCaptureMs,
                MaxCaptureMs = sample.MaxCaptureMs,
                AvgEncryptMs = sample.AvgEncryptMs,
                MaxEncryptMs = sample.MaxEncryptMs,
                AvgSendMs = sample.AvgSendMs,
                MaxSendMs = sample.MaxSendMs,
                AvgFrameKB = sample.AvgFrameKB,
                MaxFrameKB = sample.MaxFrameKB,
                BufferReadyPercent = sample.BufferReadyPercent,
                FrameCount = sample.FrameCount,
                FailedSends = sample.FailedSends,
                CaptureFailures = sample.CaptureFailures,
                LightSensorValue = sample.LightSensorValue,
                IsNight = sample.IsNight,
                LightSensorPresent = sample.LightSensorPresent,
                TemperatureC = sample.TemperatureC,
                HumidityPercent = sample.HumidityPercent,
                DewPointC = sample.DewPointC,
                TempHumiditySensorPresent = sample.TempHumiditySensorPresent,
                MotionSensorPresent = sample.MotionSensorPresent,
                CaseOpen = sample.CaseOpen,
                MotionActive = sample.MotionActive,
                MotionEvents = sample.MotionEvents
            });

            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync();

                db.CameraTelemetry.AddRange(entities);

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist {Count} camera telemetry samples.", batch.Count);
            }
        }
    }
}
