using Homewatch.Contracts.Dtos.VideoChunkDtos;
using Homewatch.Contracts.Models;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IVideoReplayService
    {
        public Task<Guid> SaveVideoChunkInfoAsync(CreateVideoChunkDto createVideoChunkDto);

        Task<List<HLSPlaylistDto>> GenerateHLSPlaylistsAsync(List<Guid> cameraIds, DateTime startTime, DateTime endTime, Guid sourceTargetId);

        Task GeneratePlaceholderChunksForMissingOnesAsync(IEnumerable<double> durationSeconds);

        Task<List<VideoChunkShortInfoDto>> GetExportTimelineSegmentsAsync(Guid cameraId, DateTime startTime, DateTime endTime, Guid? sourceTargetId);

        Task<double> GetTotalVideoChinksSizeInGBAsync();

        Task<List<ReplayFootageAvailabilityModel>> GetFootageAvailabilityForEachSourceAsync(List<Guid> cameraIds, DateTime startDate, DateTime endDate);
    }
}
