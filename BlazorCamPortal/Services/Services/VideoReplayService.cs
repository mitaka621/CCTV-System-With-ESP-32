using System.Diagnostics;
using System.Text;
using BlazorCamPortal.Contracts.Abstractions.Repositories;
using BlazorCamPortal.Contracts.Abstractions.Services;
using BlazorCamPortal.Contracts.Dtos.VideoChunkDtos;
using BlazorCamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorCamPortal.Core.Services
{
    public class VideoReplayService : IVideoReplayService
    {
        private readonly IVideoChunkRepository _videoChunkRepository;
        private readonly ILogger<IVideoReplayService> _logger;

        private readonly string _missingPlaceholderChunksPath;
        private readonly string _videoChunksBaseApiUrl;
        private readonly string _defaultFramePathOnMissingChunk;

        public VideoReplayService(
            IVideoChunkRepository videoChunkRepository,
            IConfiguration configuration,
            ILogger<IVideoReplayService> logger)
        {
            _videoChunkRepository = videoChunkRepository;
            _logger = logger;

            _missingPlaceholderChunksPath = configuration.GetSection("VideoEncoderConfig")["MissingPlaceholderChunksPath"]
                ?? throw new ArgumentNullException("MissingPlaceholderChunksPath not configured");

            _videoChunksBaseApiUrl = configuration.GetSection("VideoEncoderConfig")["VideoChunksBaseApiUrl"]
                ?? throw new ArgumentNullException("VideoChunksBaseApiUrl not configured");

            _defaultFramePathOnMissingChunk = configuration.GetSection("VideoEncoderConfig")["DefaultFramePathOnMissingChunk"]
               ?? throw new ArgumentNullException("DefaultFramePathOnMissingChunk not configured");
        }

        public async Task<Guid> SaveVideoChunkInfoAsync(CreateVideoChunkDto createVideoChunkDto)
        {
            var id = await _videoChunkRepository.CreateVideoChunkAsync(createVideoChunkDto);

            return id;
        }

        public async Task<List<HLSPlaylistDto>> GenerateHLSPlaylistsAsync(List<Guid> cameraIds, DateTime startTime, DateTime endTime)
        {
            var availableChunksByCameraId = await _videoChunkRepository.GetVideoChunksForPeriodForCameraAsync(cameraIds, startTime, endTime);

            List<HLSPlaylistDto> playlists = new();

            foreach (var cameraChunks in availableChunksByCameraId)
            {
                var cameraId = cameraChunks.Key;
                var availableChunks = cameraChunks.Value;

                if (availableChunks.Count == 0)
                {
                    continue;
                }

                List<VideoChunkDateTimeEventDto> missingVideoChunkEvents = new();

                List<VideoChunkShortInfoDto> fullTimeline = new();

                if (availableChunks.Count > 1)
                {
                    for (int i = 0; i < availableChunks.Count - 1; i++)
                    {
                        fullTimeline.Add(availableChunks[i]);

                        //compare the datetimes up to the seconds
                        if (availableChunks[i].ChunkEndDate.ToString("yyyy-MM-dd-HH-mm-ss") == availableChunks[i + 1].ChunkStartDate.ToString("yyyy-MM-dd-HH-mm-ss"))
                        {
                            continue;
                        }

                        missingVideoChunkEvents.Add(new VideoChunkDateTimeEventDto
                        {
                            EventStartTime = availableChunks[i].ChunkEndDate,
                            EventEndTime = availableChunks[i + 1].ChunkStartDate
                        });

                        var missingDuration = (availableChunks[i + 1].ChunkStartDate - availableChunks[i].ChunkEndDate).TotalSeconds;

                        fullTimeline.Add(new VideoChunkShortInfoDto()
                        {
                            FileName = string.Format(_missingPlaceholderChunksPath, missingDuration).Replace('\\', '/'),
                            ChunkStartDate = availableChunks[i].ChunkEndDate,
                            ChunkEndDate = availableChunks[i + 1].ChunkStartDate
                        });
                    }
                }

                fullTimeline.Add(availableChunks.Last());

                var sb = new StringBuilder();
                sb.AppendLine("#EXTM3U");
                sb.AppendLine("#EXT-X-VERSION:7");

                double targetDurationSeconds = fullTimeline.Max(x => (x.ChunkEndDate - x.ChunkStartDate).TotalSeconds);
                int targetDurationInteger = Math.Max(1, (int)Math.Ceiling(targetDurationSeconds));

                // HLS spec requires TARGETDURATION to be an integer, rounded to the ceiling of the longest segment
                sb.AppendLine($"#EXT-X-TARGETDURATION:{targetDurationInteger}");
                sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
                sb.AppendLine("#EXT-X-INDEPENDENT-SEGMENTS");
                sb.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");

                foreach (var chunk in fullTimeline)
                {
                    sb.AppendLine($"#EXTINF:{(chunk.ChunkEndDate - chunk.ChunkStartDate).TotalSeconds:0.###},");
                    sb.AppendLine(_videoChunksBaseApiUrl + chunk.FileName.Replace('\\', '/'));
                }

                sb.AppendLine("#EXT-X-ENDLIST");
                playlists.Add(new HLSPlaylistDto()
                {
                    CameraId = cameraId,
                    HLSPlaylistString = sb.ToString(),
                    MissingVideoChunkEvents = missingVideoChunkEvents,
                    AvailableVideoChunkEvents = availableChunks.Select(x => new VideoChunkDateTimeEventDto
                    {
                        EventStartTime = x.ChunkStartDate,
                        EventEndTime = x.ChunkEndDate
                    }).ToList()
                });
            }

            return playlists;
        }

        public async Task GeneratePlaceholderChunksForMissingOnesAsync(double durationSeconds)
        {
            var outputPath = string.Format(_missingPlaceholderChunksPath, durationSeconds);

            var directory = Path.GetDirectoryName(outputPath);

            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(outputPath))
            {
                return;
            }

            var ffmpegPath = VideoChunkUtilities.GetFfmpegPath();

            var args =
                $"-loop 1 -i \"{_defaultFramePathOnMissingChunk}\" " +
                $"-t {durationSeconds:0.###} " +
                "-map 0:v:0 -an " +
                "-c:v libx264 -preset veryfast -tune zerolatency -sc_threshold 0 " +
                "-pix_fmt yuv420p -r 25 " +
                "-f mpegts -y " +
                $"\"{outputPath}\"";

            var ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            ffmpeg.Start();

            string stderr = await ffmpeg.StandardError.ReadToEndAsync();

            ffmpeg.WaitForExit();

            if (ffmpeg.ExitCode != 0)
                _logger.LogError($"Failed to generate placeholder chunks. FFmpeg exited with code {ffmpeg.ExitCode}. Error: {stderr}");
        }

        public async Task<(DateTime, DateTime)> GetMinAndMaxDateTimeOfAvailableVideoChunksAsync()
        {
            var minTime = await _videoChunkRepository.GetMinDateTimeOfAvailableVideoChunksAsync();

            var maxTime = await _videoChunkRepository.GetMaxDateTimeOfAvailableVideoChunksAsync();

            return (minTime, maxTime);
        }
    }
}
