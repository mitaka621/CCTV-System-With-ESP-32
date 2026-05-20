using System.Diagnostics;
using System.Text;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.VideoChunkDtos;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CamPortal.Core.Services
{
    public class VideoReplayService : IVideoReplayService
    {
        private readonly IVideoChunkRepository _videoChunkRepository;
        private readonly ILogger<IVideoReplayService> _logger;

        private readonly string _missingPlaceholderChunksPath;
        private readonly string _videoChunksBaseApiUrl;
        private readonly string _defaultFramePathOnMissingChunk;
        private readonly int _encodedVideoOutputFps;
        private readonly int _maxChunksSizeInS;

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

                DateTime cursor = startTime;

                foreach (var chunk in availableChunks)
                {
                    if (chunk.ChunkStartTime > cursor)
                    {
                        FillGap(cursor, chunk.ChunkStartTime, fullTimeline, missingVideoChunkEvents);
                    }

                    fullTimeline.Add(new VideoChunkShortInfoDto
                    {
                        FileName = chunk.FileName.Replace('\\', '/'),
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
                    sb.AppendLine(_videoChunksBaseApiUrl + segment.FileName.Replace('\\', '/'));
                }

                sb.AppendLine("#EXT-X-ENDLIST");
                playlists.Add(new HLSPlaylistDto
                {
                    CameraId = cameraId,
                    HLSPlaylistString = sb.ToString(),
                    MissingVideoChunkEvents = missingVideoChunkEvents,
                    AvailableVideoChunkEvents = availableChunks.Select(x => new VideoChunkDateTimeEventDto
                    {
                        EventStartTime = x.ChunkStartTime,
                        EventEndTime = x.ChunkEndTime
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
                $"-framerate {_encodedVideoOutputFps} " +
                "-map 0:v:0 -an " +
                "-c:v libx264 -preset medium -tune zerolatency -sc_threshold 0 " +
                $"-g {_encodedVideoOutputFps * 2} -keyint_min {_encodedVideoOutputFps * 2} " +
                $"-pix_fmt yuv420p -r {_encodedVideoOutputFps} " +
                $"-t {durationSeconds} " +
                $"{outputPath}";

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
                _logger.LogError(
                    "Failed to generate placeholder chunks. FFmpeg exited with code {ExitCode}. Error: {Error}",
                    ffmpeg.ExitCode,
                    stderr);
        }

        public async Task<(DateTime, DateTime)> GetMinAndMaxDateTimeOfAvailableVideoChunksAsync()
        {
            var minTime = await _videoChunkRepository.GetMinDateTimeOfAvailableVideoChunksAsync();

            var maxTime = await _videoChunkRepository.GetMaxDateTimeOfAvailableVideoChunksAsync();

            return (minTime, maxTime);
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
            DateTime effectiveEnd = FloorToSecond(gapEnd);

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
                    FileName = string.Format(_missingPlaceholderChunksPath, segmentSeconds).Replace('\\', '/'),
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

        private static DateTime FloorToSecond(DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);
        }

        private static DateTime CeilToSecond(DateTime dt)
        {
            if (dt.Millisecond == 0 && dt.Ticks % TimeSpan.TicksPerSecond == 0)
            {
                return dt;
            }

            var floored = FloorToSecond(dt);
            return floored.AddSeconds(1);
        }
    }
}
