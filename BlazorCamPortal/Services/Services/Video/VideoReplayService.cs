using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.VideoChunkDtos;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace CamPortal.Core.Services.Video
{
    public class VideoReplayService : IVideoReplayService
    {
        private readonly IVideoChunkRepository _videoChunkRepository;
        private readonly IStorageLocationService _storageLocationService;
        private readonly ILogger<IVideoReplayService> _logger;

        private readonly string _defaultFramePathOnMissingChunk;
        private readonly int _encodedVideoOutputFps;
        private readonly int _maxChunksSizeInS;

        public VideoReplayService(
            IVideoChunkRepository videoChunkRepository,
            IStorageLocationService storageLocationService,
            IConfiguration configuration,
            ILogger<IVideoReplayService> logger)
        {
            _videoChunkRepository = videoChunkRepository;
            _storageLocationService = storageLocationService;
            _logger = logger;

            _defaultFramePathOnMissingChunk = configuration.GetSection("VideoEncoderConfig")["DefaultFramePathOnMissingChunk"]
               ?? throw new ArgumentNullException("DefaultFramePathOnMissingChunk not configured");

            _encodedVideoOutputFps = int.Parse(
                configuration.GetSection("VideoEncoderConfig")["EncodedVideoOutputFps"]
                ?? throw new ArgumentNullException("Encoded video output FPS not configured")
            );

            _maxChunksSizeInS = int.Parse(
                configuration.GetSection("VideoEncoderConfig")["VideoChunksSizeInS"]
                ?? throw new ArgumentNullException("Video chunk size not configured")
            );
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

            foreach (var cameraId in cameraIds)
            {
                List<VideoChunkDateTimeEventDto> missingVideoChunkEvents = new();

                availableChunksByCameraId.TryGetValue(cameraId, out var availableChunks);

                List<VideoChunkShortInfoDto> fullTimeline = BuildFullTimeline(cameraId, availableChunks, startTime, endTime, missingVideoChunkEvents);

                var sb = new StringBuilder();
                sb.AppendLine("#EXTM3U");
                sb.AppendLine("#EXT-X-VERSION:7");

                double targetDurationSeconds = fullTimeline.Max(x => (x.ChunkEndTime - x.ChunkStartTime).TotalSeconds);
                int targetDurationInteger = Math.Max(1, (int)Math.Ceiling(targetDurationSeconds));

                // HLS spec requires TARGETDURATION to be an integer, rounded to the ceiling of the longest segment
                sb.AppendLine($"#EXT-X-TARGETDURATION:{targetDurationInteger}");
                sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
                sb.AppendLine("#EXT-X-INDEPENDENT-SEGMENTS");
                sb.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");

                foreach (var segment in fullTimeline)
                {
                    sb.AppendLine("#EXT-X-DISCONTINUITY");
                    sb.AppendLine($"#EXTINF:{(segment.ChunkEndTime - segment.ChunkStartTime).TotalSeconds:0.###},");
                    sb.AppendLine(_storageLocationService.BuildChunkUrl(segment.CameraFolder!, segment.FileName));
                }

                sb.AppendLine("#EXT-X-ENDLIST");
                playlists.Add(new HLSPlaylistDto
                {
                    CameraId = cameraId,
                    HLSPlaylistString = sb.ToString(),
                    MissingVideoChunkEvents = missingVideoChunkEvents,
                    AvailableVideoChunkEvents = availableChunks?.Select(x => new VideoChunkDateTimeEventDto
                    {
                        EventStartTime = x.ChunkStartTime,
                        EventEndTime = x.ChunkEndTime
                    }).ToList() ?? new List<VideoChunkDateTimeEventDto>()
                });
            }

            return playlists;
        }

        public async Task<List<string>> GetExportTimelineSegmentsAsync(Guid cameraId, DateTime startTime, DateTime endTime)
        {
            var availableChunksByCameraId = await _videoChunkRepository
                .GetVideoChunksForPeriodForCameraAsync(new List<Guid> { cameraId }, startTime, endTime);

            availableChunksByCameraId.TryGetValue(cameraId, out var availableChunks);

            List<VideoChunkDateTimeEventDto> missingVideoChunkEvents = new();

            var fullTimeline = BuildFullTimeline(cameraId, availableChunks, startTime, endTime, missingVideoChunkEvents);

            var missingDurations = missingVideoChunkEvents
                .Select(x => x.TotalDuration)
                .Distinct();

            await GeneratePlaceholderChunksForMissingOnesAsync(missingDurations);

            return fullTimeline
                .Select(x => _storageLocationService.GetChunkFullPath(x.CameraFolder!, x.FileName))
                .ToList();
        }

        public async Task GeneratePlaceholderChunksForMissingOnesAsync(IEnumerable<double> durationSeconds)
        {
            List<Task> FfmpegTasks = new();

            foreach (var duration in durationSeconds)
            {
                var outputPath = _storageLocationService.GetPlaceholderChunkFullPath(duration);

                var directory = Path.GetDirectoryName(outputPath);

                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                {
                    continue;
                }

                var ffmpegPath = VideoChunkUtilities.GetFfmpegPath();

                var args =
                    $"-y -framerate {_encodedVideoOutputFps} -loop 1 -i \"{_defaultFramePathOnMissingChunk}\" " +
                    "-vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2,setsar=1\" " +
                    "-map 0:v:0 -an " +
                    "-c:v libx264 -preset medium -tune zerolatency -sc_threshold 0 " +
                    $"-g {_encodedVideoOutputFps * 2} -keyint_min {_encodedVideoOutputFps * 2} " +
                    $"-pix_fmt yuv420p -r {_encodedVideoOutputFps} " +
                    $"-t {duration} " +
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

                if (ffmpeg.ExitCode != 0)
                    _logger.LogError(
                        "Failed to generate placeholder chunks. FFmpeg exited with code {ExitCode}. Error: {Error}",
                        ffmpeg.ExitCode,
                        stderr);

                FfmpegTasks.Add(ffmpeg.WaitForExitAsync());
            }

            await Task.WhenAll(FfmpegTasks);
        }

        public async Task<double> GetTotalVideoChinksSizeInGBAsync()
        {
            var chunksInMB = await _videoChunkRepository.GetTotalVideoChinksSizeInMBAsync();

            return chunksInMB / 1024.0;
        }

        private void FillGap(DateTime gapStart, DateTime gapEnd, List<VideoChunkShortInfoDto> fullTimeline, List<VideoChunkDateTimeEventDto> missingVideoChunkEvents)
        {
            if (gapEnd <= gapStart)
            {
                return;
            }

            // Only consider whole-second gaps to avoid flicker from sub-second gaps
            DateTime effectiveStart = CeilToSecond(gapStart);
            DateTime effectiveEnd = MiscUtilities.FloorToSecond(gapEnd);

            int missingSeconds = (int)(effectiveEnd - effectiveStart).TotalSeconds;
            if (missingSeconds <= 0)
            {
                return;
            }

            DateTime current = effectiveStart;

            while (missingSeconds > 0)
            {
                int segmentSeconds = Math.Min(_maxChunksSizeInS, missingSeconds);

                fullTimeline.Add(new VideoChunkShortInfoDto
                {
                    FileName = _storageLocationService.GetPlaceholderChunkFileName(segmentSeconds),
                    CameraFolder = _storageLocationService.PlaceholderFolderName,
                    ChunkStartTime = current,
                    ChunkEndTime = current.AddSeconds(segmentSeconds)
                });

                missingVideoChunkEvents.Add(new VideoChunkDateTimeEventDto
                {
                    EventStartTime = current,
                    EventEndTime = current.AddSeconds(segmentSeconds)
                });

                current = current.AddSeconds(segmentSeconds);
                missingSeconds -= segmentSeconds;
            }
        }

        private DateTime CeilToSecond(DateTime dt)
        {
            if (dt.Millisecond == 0 && dt.Ticks % TimeSpan.TicksPerSecond == 0)
            {
                return dt;
            }

            var floored = MiscUtilities.FloorToSecond(dt);
            return floored.AddSeconds(1);
        }

        private List<VideoChunkShortInfoDto> BuildFullTimeline(
            Guid cameraId,
            List<VideoChunkShortInfoDto>? availableChunks,
            DateTime startTime,
            DateTime endTime,
            List<VideoChunkDateTimeEventDto> missingVideoChunkEvents)
        {
            List<VideoChunkShortInfoDto> fullTimeline = new();

            if (availableChunks == null || availableChunks.Count == 0)
            {
                FillGap(startTime, endTime, fullTimeline, missingVideoChunkEvents);

                return fullTimeline;
            }

            DateTime cursor = startTime;

            foreach (var chunk in availableChunks)
            {
                if (chunk.ChunkStartTime > cursor)
                {
                    FillGap(cursor, chunk.ChunkStartTime, fullTimeline, missingVideoChunkEvents);
                }

                fullTimeline.Add(new VideoChunkShortInfoDto
                {
                    FileName = Path.GetFileName(chunk.FileName),
                    CameraFolder = cameraId.ToString(),
                    ChunkStartTime = chunk.ChunkStartTime,
                    ChunkEndTime = chunk.ChunkEndTime
                });

                if (chunk.ChunkEndTime > cursor)
                {
                    cursor = chunk.ChunkEndTime;
                }
            }

            if (cursor < endTime)
            {
                FillGap(cursor, endTime, fullTimeline, missingVideoChunkEvents);
            }

            return fullTimeline;
        }
    }
}
