using CamPortal.Contracts.Dtos.VideoChunkDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IVideoReplayService
    {
        public Task<Guid> SaveVideoChunkInfoAsync(CreateVideoChunkDto createVideoChunkDto);

        Task<List<HLSPlaylistDto>> GenerateHLSPlaylistsAsync(List<Guid> cameraId, DateTime startTime, DateTime endTime);

        Task GeneratePlaceholderChunksForMissingOnesAsync(IEnumerable<double> durationSeconds);

        Task<List<string>> GetExportTimelineSegmentsAsync(Guid cameraId, DateTime startTime, DateTime endTime);

        Task<double> GetTotalVideoChinksSizeInGBAsync();
    }
}
