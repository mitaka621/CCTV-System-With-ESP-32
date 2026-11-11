using CamPortal.Contracts.Dtos.RemoteStorageDtos;
using CamPortal.Contracts.Dtos.VideoChunkDtos;

namespace CamPortal.Contracts.Abstractions.Repositories
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
