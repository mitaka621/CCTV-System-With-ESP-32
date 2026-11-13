using Homewatch.Contracts.Dtos.RemoteStorageDtos;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IChunkReplicationQueue
    {
        void Enqueue(VideoChunkReplicationItemDto item);
    }
}
