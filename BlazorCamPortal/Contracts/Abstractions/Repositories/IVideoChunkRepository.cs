using CamPortal.Contracts.Dtos.RemoteStorageDtos;
using CamPortal.Contracts.Dtos.VideoChunkDtos;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface IVideoChunkRepository
    {
        Task<Guid> CreateVideoChunkAsync(CreateVideoChunkDto createVideoChunkDto);

        Task<Dictionary<Guid, List<VideoChunkShortInfoDto>>> GetVideoChunksForPeriodForCameraFromLocalStorageAsync(List<Guid> cameraId, DateTime startDate, DateTime endDate);

        Task<double> GetTotalVideoChinksSizeInMBAsync();

        Task<List<ExpiredVideoChunkDto>> GetExpiredVideoChunksAsync(DateTime expirationCutoffUtc);

        Task MarkVideoChunksAsDeletedInLocalStorageAsync(List<Guid> chunkIds);

        Task<List<VideoChunkReplicationItemDto>> GetUnreplicatedVideoChunksAsync(Guid targetId);

        Task<int> DeleteChunksWhichAreNotAvailbileInAnyStorageLocationAsync();

        Task<int> CountTotalAvailableChunksForPeriodInLocalStorageAsync(List<Guid> cameraId, DateTime startDate, DateTime endDate);
    }
}
