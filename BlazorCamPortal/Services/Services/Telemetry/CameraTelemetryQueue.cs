using CamPortal.Contracts.Dtos.TelemetryDtos;
using System.Threading.Channels;

namespace CamPortal.Core.Services.Telemetry
{
    public sealed class CameraTelemetryQueue
    {
        private readonly Channel<CameraTelemetrySampleDto> _channel = Channel.CreateBounded<CameraTelemetrySampleDto>(
            new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });

        public ChannelWriter<CameraTelemetrySampleDto> Writer => _channel.Writer;

        public ChannelReader<CameraTelemetrySampleDto> Reader => _channel.Reader;

        public void Complete() => _channel.Writer.TryComplete();
    }
}
