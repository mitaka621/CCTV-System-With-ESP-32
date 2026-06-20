using CamPortal.Contracts.Dtos.VideoChunkDtos;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface IVideoChunkRepository
    {
        Task<Guid> CreateVideoChunkAsync(CreateVideoChunkDto createVideoChunkDto);

        Task<Dictionary<Guid, List<VideoChunkShortInfoDto>>> GetVideoChunksForPeriodForCameraAsync(List<Guid> cameraId, DateTime startDate, DateTime endDate);

        Task<DateTime> GetMinDateTimeOfAvailableVideoChunksAsync();

        Task<DateTime> GetMaxDateTimeOfAvailableVideoChunksAsync();

        Task<double> GetTotalVideoChinksSizeInMBAsync();

        Task<List<ExpiredVideoChunkDto>> GetExpiredVideoChunksAsync(DateTime expirationCutoffUtc);

        Task DeleteVideoChunksAsync(List<Guid> chunkIds);
    }
}
