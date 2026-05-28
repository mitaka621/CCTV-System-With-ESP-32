using CamPortal.Core.LoggerProviders.DatabaseLogger;
using CamPortal.Infrastructure.Data;
using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace CamPortal.Core.BackgroundServices
{
    public class LoggerDatabaseWriterService : BackgroundService
    {
        private const int _batchSize = 50;
        private const int _waitTimeInSeconds = 5;

        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly DatabaseLogQueue _queue;

        public LoggerDatabaseWriterService(DatabaseLogQueue queue, IDbContextFactory<CamPortalDBContext> dbContextFactory)
        {
            _queue = queue;
            _dbContextFactory = dbContextFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                List<LogMessage> batch = new List<LogMessage>();

                var nextBatchWrite = DateTime.UtcNow.AddSeconds(_waitTimeInSeconds);

                while (nextBatchWrite > DateTime.UtcNow && batch.Count < _batchSize && !stoppingToken.IsCancellationRequested)
                {
                    batch.Add(await _queue.Reader.ReadAsync(stoppingToken));
                }

                await WriteMessagesToDatabaseAsync(batch);
            }

            _queue.Complete();

            List<LogMessage> endBatch = new List<LogMessage>();

            await foreach (var logMessage in _queue.Reader.ReadAllAsync())
            {
                endBatch.Add(logMessage);
            }

            await WriteMessagesToDatabaseAsync(endBatch);
        }

        private async Task WriteMessagesToDatabaseAsync(List<LogMessage> batch)
        {
            var db = await _dbContextFactory.CreateDbContextAsync();

            db.LogMessages.AddRange(batch.OrderByDescending(x => x.TimestampUTC));

            try
            {
                await db.SaveChangesAsync();
            }
            catch
            {
            }
        }
    }
}
