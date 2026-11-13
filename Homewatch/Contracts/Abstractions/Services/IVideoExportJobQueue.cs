using System.Threading.Channels;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IVideoExportJobQueue
    {
        ChannelReader<Guid> Reader { get; }

        void Enqueue(Guid exportId);
    }
}
