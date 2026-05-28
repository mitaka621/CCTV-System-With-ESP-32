using CamPortal.Infrastructure.Data.Entities;
using System.Threading.Channels;

namespace CamPortal.Core.LoggerProviders.DatabaseLogger
{
    public sealed class DatabaseLogQueue
    {
        private readonly Channel<LogMessage> _channel = Channel.CreateBounded<LogMessage>(
            new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });

        public ChannelWriter<LogMessage> Writer => _channel.Writer;
        public ChannelReader<LogMessage> Reader => _channel.Reader;

        public void Complete() => _channel.Writer.TryComplete();
    }
}
