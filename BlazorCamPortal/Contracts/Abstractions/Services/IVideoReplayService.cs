using BlazorCamPortal.Contracts.Dtos.VideoChunkDtos;

namespace BlazorCamPortal.Contracts.Abstractions.Services
{
    public interface IVideoReplayService
    {
        public Task<Guid> SaveVideoChunkInfoAsync(CreateVideoChunkDto createVideoChunkDto);

        Task<List<HLSPlaylistDto>> GenerateHLSPlaylistsAsync(List<Guid> cameraId, DateTime startTime, DateTime endTime);

        Task GeneratePlaceholderChunksForMissingOnesAsync(double durationSeconds);

        Task<(DateTime, DateTime)> GetMinAndMaxDateTimeOfAvailableVideoChunksAsync();

        Task<double> GetTotalVideoChinksSizeInGBAsync();
    }
}
