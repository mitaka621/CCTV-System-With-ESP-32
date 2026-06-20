using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CamPortal.Core.BackgroundServices
{
    public class VideoRetentionCleanupService : BackgroundService
    {
        private readonly IExportedVideoRepository _exportedVideoRepository;
        private readonly IVideoChunkRepository _videoChunkRepository;
        private readonly ISystemSettingsService _systemSettingsService;
        private readonly IVideoExportNotifier _videoExportNotifier;
        private readonly IStorageLocationService _storageLocationService;
        private readonly ILogger<VideoRetentionCleanupService> _logger;

        private readonly TimeSpan _cleanupInterval;

        public VideoRetentionCleanupService(
            IExportedVideoRepository exportedVideoRepository,
            IVideoChunkRepository videoChunkRepository,
            ISystemSettingsService systemSettingsService,
            IVideoExportNotifier videoExportNotifier,
            IStorageLocationService storageLocationService,
            IConfiguration configuration,
            ILogger<VideoRetentionCleanupService> logger)
        {
            _exportedVideoRepository = exportedVideoRepository;
            _videoChunkRepository = videoChunkRepository;
            _systemSettingsService = systemSettingsService;
            _videoExportNotifier = videoExportNotifier;
            _storageLocationService = storageLocationService;
            _logger = logger;

            var cleanupIntervalInMinutes = int.Parse(
                configuration.GetSection("VideoExportConfig")["CleanupIntervalInMinutes"]
                ?? throw new ArgumentNullException("CleanupIntervalInMinutes not configured"));

            _cleanupInterval = TimeSpan.FromMinutes(cleanupIntervalInMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_cleanupInterval);

            do
            {
                try
                {
                    await RunCleanupAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Video retention cleanup run failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task RunCleanupAsync()
        {
            var settings = await _systemSettingsService.GetSystemSettingsAsync();

            await CleanupExpiredExportedVideosAsync(settings.EncodedVideoRetention.ToTimeSpan());

            await CleanupExpiredCameraChunksAsync(settings.CameraChunkRetention.ToTimeSpan());
        }

        private async Task CleanupExpiredExportedVideosAsync(TimeSpan? retention)
        {
            if (retention == null)
            {
                return;
            }

            var cutoffUtc = DateTime.UtcNow - retention.Value;

            var expiredExports = await _exportedVideoRepository.GetExpiredExportsAsync(cutoffUtc);

            if (expiredExports.Count == 0)
            {
                return;
            }

            foreach (var expiredExport in expiredExports)
            {
                if (!string.IsNullOrEmpty(expiredExport.FilePath))
                {
                    TryDeleteFile(_storageLocationService.GetExportFullPath(Path.GetFileName(expiredExport.FilePath)));
                }
            }

            var expiredIds = expiredExports.Select(x => x.Id).ToList();

            await _exportedVideoRepository.MarkExportsAsExpiredAsync(expiredIds);

            foreach (var expiredId in expiredIds)
            {
                var updatedExport = await _exportedVideoRepository.GetExportByIdAsync(expiredId);

                if (updatedExport != null)
                {
                    _videoExportNotifier.NotifyExportStatusChanged(updatedExport);
                }
            }

            _logger.LogInformation("Expired {Count} encoded videos during retention cleanup.", expiredIds.Count);
        }

        private async Task CleanupExpiredCameraChunksAsync(TimeSpan? retention)
        {
            if (retention == null)
            {
                return;
            }

            var cutoffUtc = DateTime.UtcNow - retention.Value;

            var expiredChunks = await _videoChunkRepository.GetExpiredVideoChunksAsync(cutoffUtc);

            if (expiredChunks.Count == 0)
            {
                return;
            }

            foreach (var expiredChunk in expiredChunks)
            {
                TryDeleteFile(_storageLocationService.GetChunkFullPath(
                    expiredChunk.DeviceId.ToString(),
                    Path.GetFileName(expiredChunk.FileName)));
            }

            await _videoChunkRepository.DeleteVideoChunksAsync(expiredChunks.Select(x => x.Id).ToList());

            _logger.LogInformation("Deleted {Count} expired camera chunks during retention cleanup.", expiredChunks.Count);
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
                _logger.LogWarning(ex, "Failed to delete file {Path} during retention cleanup.", path);
            }
        }
    }
}
