using CamPortal.Infrastructure.Data.Entities;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace CamPortal.Core.LoggerProviders.DatabaseLogger
{
    public sealed class DatabaseLogger : ILogger
    {
        private readonly string _category;
        private readonly ChannelWriter<LogMessage> _writer;

        public DatabaseLogger(string category, ChannelWriter<LogMessage> writer)
        {
            _category = category;
            _writer = writer;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _writer.TryWrite(new LogMessage
            {
                Id = Guid.NewGuid(),
                TimestampUTC = DateTime.UtcNow,
                LogLevel = logLevel,
                Category = LogMessageFieldFormatter.FormatCategory(_category),
                Message = LogMessageFieldFormatter.FormatMessage(formatter(state, exception)),
                Exception = LogMessageFieldFormatter.FormatException(exception?.ToString())
            });
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => null;
    }
}
