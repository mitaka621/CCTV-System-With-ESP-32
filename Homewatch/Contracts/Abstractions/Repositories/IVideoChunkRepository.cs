using Homewatch.Contracts.Dtos.RemoteStorageDtos;
using Homewatch.Contracts.Dtos.VideoChunkDtos;

namespace Homewatch.Contracts.Abstractions.Repositories
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
    }
}
