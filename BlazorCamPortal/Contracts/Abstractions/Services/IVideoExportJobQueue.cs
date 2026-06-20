using System.Threading.Channels;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IVideoExportJobQueue
    {
        ChannelReader<Guid> Reader { get; }

        void Enqueue(Guid exportId);
    }
}
