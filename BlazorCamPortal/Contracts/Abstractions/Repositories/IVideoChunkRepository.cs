using BlazorCamPortal.Contracts.Dtos.VideoChunkDtos;

namespace BlazorCamPortal.Contracts.Abstractions.Repositories
{
    public interface IVideoChunkRepository
    {
        Task<Guid> CreateVideoChunkAsync(CreateVideoChunkDto createVideoChunkDto);

        Task<List<VideoChunkShortInfoDto>> GetVideoChunksForPeriodForCameraAsync(Guid cameraId, DateTime startDate, DateTime endDate);
    }
}
