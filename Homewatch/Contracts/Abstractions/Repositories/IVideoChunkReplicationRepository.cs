using Homewatch.Contracts.Dtos.RemoteStorageDtos;
using Homewatch.Contracts.Dtos.VideoChunkDtos;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface IVideoChunkReplicationRepository
    {
        Task<bool> IsVideoChunkReplicatedAsync(Guid targetId, Guid videoChunkId);

        Task MarkVideoChunkReplicatedAsync(Guid targetId, Guid videoChunkId);

        Task<List<ExpiredVideoChunkDto>> GetAllExpiredReplicatedChunksForTargetAsync(Guid targetId, DateTime cutoffUTC);

        Task<int> MarkReplicatedChunksAsDeletedAsync(Guid targetId, List<Guid> videChunkIds);

        Task<Dictionary<Guid, List<VideoChunkShortInfoDto>>> GetVideoChunksForPeriodForCameraFromRemoteStorageAsync(Guid targetId, List<Guid> cameraId, DateTime startDate, DateTime endDate);
    }
}
