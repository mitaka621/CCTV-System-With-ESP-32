using Microsoft.Extensions.Logging;

namespace CamPortal.Core.LoggerProviders.DatabaseLogger
{
    public sealed class DatabaseLoggerProvider : ILoggerProvider
    {
        private readonly DatabaseLogQueue _queue;

        public DatabaseLoggerProvider(DatabaseLogQueue queue)
        {
            _queue = queue;
        }

        public ILogger CreateLogger(string categoryName)
            => new DatabaseLogger(categoryName, _queue.Writer);

        public void Dispose()
        {
        }
    }
}
