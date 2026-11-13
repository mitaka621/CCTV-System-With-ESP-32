using Homewatch.Contracts.Abstractions.Services;
using System.Threading.Channels;

namespace Homewatch.Core.Services.Video
{
    public class VideoExportJobQueue : IVideoExportJobQueue
    {
        private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

        public ChannelReader<Guid> Reader => _channel.Reader;

        public void Enqueue(Guid exportId)
        {
            _channel.Writer.TryWrite(exportId);
        }
    }
}
