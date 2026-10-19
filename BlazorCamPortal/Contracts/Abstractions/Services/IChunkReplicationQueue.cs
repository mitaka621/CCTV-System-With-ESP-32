using CamPortal.Contracts.Dtos.RemoteStorageDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IChunkReplicationQueue
    {
        void Enqueue(VideoChunkReplicationItemDto item);
    }
}
