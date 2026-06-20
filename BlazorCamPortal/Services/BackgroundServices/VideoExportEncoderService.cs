using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.ExportedVideoDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CamPortal.Core.BackgroundServices
{
    public class VideoExportEncoderService : BackgroundService, IVideoExportCanceller
    {
        private readonly IVideoExportJobQueue _videoExportJobQueue;
        private readonly IExportedVideoRepository _exportedVideoRepository;
        private readonly IVideoReplayService _videoReplayService;
        private readonly IVideoExportNotifier _videoExportNotifier;
        private readonly IStorageLocationService _storageLocationService;
        private readonly ILogger<VideoExportEncoderService> _logger;

        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningExports = new();
        private readonly ConcurrentDictionary<Guid, byte> _userCancelledExportIds = new();

        public VideoExportEncoderService(
            IVideoExportJobQueue videoExportJobQueue,
            IExportedVideoRepository exportedVideoRepository,
            IVideoReplayService videoReplayService,
            IVideoExportNotifier videoExportNotifier,
            IStorageLocationService storageLocationService,
            ILogger<VideoExportEncoderService> logger)
        {
            _videoExportJobQueue = videoExportJobQueue;
            _exportedVideoRepository = exportedVideoRepository;
            _videoReplayService = videoReplayService;
            _videoExportNotifier = videoExportNotifier;
            _storageLocationService = storageLocationService;
            _logger = logger;
        }

        public void RequestCancel(Guid exportId)
        {
            _userCancelledExportIds[exportId] = 0;

            if (_runningExports.TryGetValue(exportId, out var cts))
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var exportId in _videoExportJobQueue.Reader.ReadAllAsync(stoppingToken))
            {
                if (_userCancelledExportIds.TryRemove(exportId, out _))
                {
                    await RemoveCancelledExportAsync(exportId);
                    continue;
                }

                using var exportCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _runningExports[exportId] = exportCts;

                if (_userCancelledExportIds.ContainsKey(exportId))
                {
                    exportCts.Cancel();
                }

                try
                {
                    await ProcessExportAsync(exportId, exportCts.Token);
                }
                catch (OperationCanceledException)
                {
                    var userCancelled = _userCancelledExportIds.TryRemove(exportId, out _);

                    if (userCancelled)
                    {
                        await RemoveCancelledExportAsync(exportId);
                    }

                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception while processing video export {ExportId}", exportId);

                    await FinishWithStatusAsync(exportId, ExportVideoStatuses.Failed, null, null, 0);
                }
                finally
                {
                    _runningExports.TryRemove(exportId, out _);
                    _userCancelledExportIds.TryRemove(exportId, out _);
                }
            }
        }

        private async Task RemoveCancelledExportAsync(Guid exportId)
        {
            var export = await _exportedVideoRepository.GetExportByIdAsync(exportId);

            if (export == null)
            {
                return;
            }

            var outputPath = _storageLocationService.GetExportFullPath($"{exportId}.mp4");

            TryDeleteFile(outputPath);

            await _exportedVideoRepository.DeleteExportAsync(exportId);

            _videoExportNotifier.NotifyExportRemoved(new VideoExportRemovedDto
            {
                ExportId = exportId,
                UserId = export.UserId
            });

            _logger.LogInformation("Video export {ExportId} was cancelled and removed.", exportId);
        }

        private async Task ProcessExportAsync(Guid exportId, CancellationToken stoppingToken)
        {
            var export = await _exportedVideoRepository.GetExportByIdAsync(exportId);

            if (export == null)
            {
                _logger.LogWarning("Video export {ExportId} was not found in the database.", exportId);
                return;
            }

            var segmentPaths = await _videoReplayService.GetExportTimelineSegmentsAsync(
                export.CameraId,
                export.VideoStartDate,
                export.VideoEndDate);

            var existingChunkPaths = segmentPaths
                .Where(File.Exists)
                .ToList();

            if (existingChunkPaths.Count == 0)
            {
                _logger.LogWarning("No timeline segments available to export for {ExportId}.", exportId);

                await FinishWithStatusAsync(exportId, ExportVideoStatuses.Failed, null, null, 0);
                return;
            }

            var outputFileName = $"{exportId}.mp4";
            var outputPath = _storageLocationService.GetExportFullPath(outputFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var concatListPath = _storageLocationService.GetExportFullPath($"{exportId}.txt");

            await WriteConcatListFileAsync(concatListPath, existingChunkPaths);

            var encodingInput = new VideoExportEncodingInputDto
            {
                ExportId = exportId,
                UserId = export.UserId,
                TotalSeconds = Math.Max(0, (export.VideoEndDate - export.VideoStartDate).TotalSeconds),
                ConcatListPath = concatListPath,
                OutputPath = outputPath
            };

            try
            {
                var success = await RunFfmpegConcatAsync(encodingInput, copyStreams: true, stoppingToken);

                if (!success)
                {
                    success = await RunFfmpegConcatAsync(encodingInput, copyStreams: false, stoppingToken);
                }

                if (!success || !File.Exists(outputPath))
                {
                    TryDeleteFile(outputPath);

                    await FinishWithStatusAsync(exportId, ExportVideoStatuses.Failed, null, null, 0);
                    return;
                }

                var sizeInMB = (int)Math.Round(new FileInfo(outputPath).Length / (1024.0 * 1024.0));

                var downloadUrl = _storageLocationService.BuildExportUrl(outputFileName);

                await FinishWithStatusAsync(exportId, ExportVideoStatuses.Finished, downloadUrl, outputFileName, sizeInMB);
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(outputPath);

                throw;
            }
            finally
            {
                TryDeleteFile(concatListPath);
            }
        }

        private async Task WriteConcatListFileAsync(string concatListPath, List<string> chunkPaths)
        {
            var lines = chunkPaths.Select(path =>
            {
                var normalized = path.Replace('\\', '/').Replace("'", "'\\''");
                return $"file '{normalized}'";
            });

            await File.WriteAllLinesAsync(concatListPath, lines);
        }

        private async Task<bool> RunFfmpegConcatAsync(VideoExportEncodingInputDto input, bool copyStreams, CancellationToken stoppingToken)
        {
            var codecSection = copyStreams
                ? "-c copy "
                : "-c:v libx264 -preset veryfast -pix_fmt yuv420p ";

            var arguments =
                $"-y -progress pipe:1 -nostats -f concat -safe 0 -i \"{input.ConcatListPath}\" -an {codecSection}-movflags +faststart \"{input.OutputPath}\"";

            var ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = VideoChunkUtilities.GetFfmpegPath(),
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            ffmpeg.Start();

            var progressTask = TrackFfmpegProgressAsync(ffmpeg, input);

            var stderrTask = ffmpeg.StandardError.ReadToEndAsync();

            try
            {
                await ffmpeg.WaitForExitAsync(stoppingToken);
            }
            finally
            {
                TryKillProcess(ffmpeg);
            }

            await progressTask;

            var stderr = await stderrTask;

            if (ffmpeg.ExitCode != 0)
            {
                _logger.LogError(
                    "FFmpeg export failed (copy={Copy}) with exit code {ExitCode}. Output: {Error}",
                    copyStreams,
                    ffmpeg.ExitCode,
                    stderr);

                return false;
            }

            return true;
        }

        private async Task TrackFfmpegProgressAsync(Process ffmpeg, VideoExportEncodingInputDto input)
        {
            const double minSecondsBetweenUpdates = 0.75;

            double encodedSeconds = 0;
            double? speed = null;
            DateTime lastEmit = DateTime.MinValue;

            string? line;
            while ((line = await ffmpeg.StandardOutput.ReadLineAsync()) != null)
            {
                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();

                switch (key)
                {
                    case "out_time_us":
                    case "out_time_ms":
                        if (long.TryParse(value, out var microseconds) && microseconds > 0)
                        {
                            encodedSeconds = microseconds / 1_000_000.0;
                        }
                        break;
                    case "speed":
                        var speedToken = value.Replace("x", string.Empty).Trim();
                        if (double.TryParse(speedToken, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedSpeed) && parsedSpeed > 0)
                        {
                            speed = parsedSpeed;
                        }
                        break;
                    case "progress":
                        var isEnd = value == "end";

                        if (isEnd || (DateTime.UtcNow - lastEmit).TotalSeconds >= minSecondsBetweenUpdates)
                        {
                            lastEmit = DateTime.UtcNow;
                            EmitProgress(input, encodedSeconds, speed, isEnd);
                        }
                        break;
                }
            }
        }

        private void EmitProgress(VideoExportEncodingInputDto input, double encodedSeconds, double? speed, bool isEnd)
        {
            double percent;
            double? secondsRemaining;

            if (input.TotalSeconds > 0)
            {
                percent = Math.Clamp(encodedSeconds / input.TotalSeconds * 100.0, 0, 100);

                var remainingMediaSeconds = Math.Max(0, input.TotalSeconds - encodedSeconds);

                secondsRemaining = speed is > 0 ? remainingMediaSeconds / speed.Value : null;
            }
            else
            {
                percent = isEnd ? 100 : 0;
                secondsRemaining = null;
            }

            if (isEnd)
            {
                percent = 100;
                secondsRemaining = 0;
            }

            _videoExportNotifier.NotifyExportProgressChanged(new VideoExportProgressDto
            {
                ExportId = input.ExportId,
                UserId = input.UserId,
                ProgressPercent = Math.Round(percent, 1),
                EncodedSeconds = encodedSeconds,
                TotalSeconds = input.TotalSeconds,
                EstimatedSecondsRemaining = secondsRemaining
            });
        }

        private async Task FinishWithStatusAsync(Guid exportId, ExportVideoStatuses status, string? downloadUrl, string? filePath, int sizeInMB)
        {
            await _exportedVideoRepository.FinishExportAsync(new FinishVideoExportDto
            {
                Id = exportId,
                ExportStatus = status,
                ExportedURLForDownload = downloadUrl,
                FilePath = filePath,
                ExportFinishedDate = DateTime.UtcNow,
                SizeInMB = sizeInMB
            });

            var updatedExport = await _exportedVideoRepository.GetExportByIdAsync(exportId);

            if (updatedExport != null)
            {
                _videoExportNotifier.NotifyExportStatusChanged(updatedExport);
            }
        }

        private void TryKillProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to terminate ffmpeg export process.");
            }
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary file {Path}", path);
            }
        }
    }
}
