using BlazorCamPortal.Contracts.Dtos.VideoChunkDtos;

namespace BlazorCamPortal.Contracts.Abstractions.Repositories
{
    public interface IVideoChunkRepository
    {
        Task<Guid> CreateVideoChunkAsync(CreateVideoChunkDto createVideoChunkDto);

        Task<Dictionary<Guid, List<VideoChunkShortInfoDto>>> GetVideoChunksForPeriodForCameraAsync(List<Guid> cameraId, DateTime startDate, DateTime endDate);

        Task<DateTime> GetMinDateTimeOfAvailableVideoChunksAsync();

        Task<DateTime> GetMaxDateTimeOfAvailableVideoChunksAsync();

        Task<double> GetTotalVideoChinksSizeInMBAsync();
    }
}
