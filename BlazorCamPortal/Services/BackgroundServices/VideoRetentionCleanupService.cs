using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.RemoteStorageDtos;
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
        private readonly IRemoteStorageTargetRepository _remoteStorageTargetRepository;
        private readonly ISmbConnectionService _smbConnectionService;
        private readonly IVideoChunkReplicationRepository _videoChunkReplicationRepository;

        private readonly TimeSpan _cleanupInterval;

        public VideoRetentionCleanupService(
            IExportedVideoRepository exportedVideoRepository,
            IVideoChunkRepository videoChunkRepository,
            ISystemSettingsService systemSettingsService,
            IVideoExportNotifier videoExportNotifier,
            IStorageLocationService storageLocationService,
            IConfiguration configuration,
            ILogger<VideoRetentionCleanupService> logger,
            IRemoteStorageTargetRepository remoteStorageTargetRepository,
            ISmbConnectionService smbConnectionService,
            IVideoChunkReplicationRepository videoChunkReplicationRepository)
        {
            _exportedVideoRepository = exportedVideoRepository;
            _videoChunkRepository = videoChunkRepository;
            _systemSettingsService = systemSettingsService;
            _videoExportNotifier = videoExportNotifier;
            _storageLocationService = storageLocationService;
            _logger = logger;
            _remoteStorageTargetRepository = remoteStorageTargetRepository;
            _videoChunkReplicationRepository = videoChunkReplicationRepository;

            var cleanupIntervalInMinutes = int.Parse(
                configuration.GetSection("VideoExportConfig")["CleanupIntervalInMinutes"]
                ?? throw new ArgumentNullException("CleanupIntervalInMinutes not configured"));

            _cleanupInterval = TimeSpan.FromMinutes(cleanupIntervalInMinutes);
            _smbConnectionService = smbConnectionService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_cleanupInterval);

            do
            {
                try
                {
                    _logger.LogInformation("Video retention cleanup started.");

                    await RunCleanupAsync();

                    _logger.LogInformation("Video retention cleanup finished.");
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

            await CleanupExpiredCameraChunksFromLocalStorageAsync(settings.CameraChunkRetention.ToTimeSpan());

            var enabledTargets = await _remoteStorageTargetRepository.GetEnabledAsync();
            foreach (var remoteTarget in enabledTargets.Values)
            {
                await CleanupExpiredCameraChunksFromRemoteStorageAsync(remoteTarget);
            }

            await DeleteDbVideoChunkRecordsWithNoFootagePresentAnywhereAsync();
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

            _logger.LogInformation("{Count} expired, encoded videos cleaned during retention cleanup.", expiredIds.Count);
        }

        private async Task CleanupExpiredCameraChunksFromLocalStorageAsync(TimeSpan? retention)
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
                if (_storageLocationService.TryGetCameraChunkFullPath(
                        expiredChunk.DeviceId.ToString(),
                        expiredChunk.FileName,
                        out var chunkPath))
                {
                    TryDeleteFile(chunkPath);
                }
            }

            await _videoChunkRepository.MarkVideoChunksAsDeletedInLocalStorageAsync(expiredChunks.Select(x => x.Id).ToList());

            _logger.LogInformation("Deleted and marked {Count} expired camera chunks from the server's local storage during retention cleanup.", expiredChunks.Count);
        }

        private async Task CleanupExpiredCameraChunksFromRemoteStorageAsync(RemoteStorageTargetDto target)
        {
            using var session = _smbConnectionService.OpenSession(target);

            if (!session.IsConnected)
            {
                _logger.LogError("Failed to connect to {SmbShare}", target.SharePath);
                return;
            }

            var cutoffUtc = DateTime.UtcNow - target.Retention.ToTimeSpan();

            var chunksToDelete = await _videoChunkReplicationRepository.GetAllExpiredReplicatedChunksForTargetAsync(target.Id, cutoffUtc!.Value);

            foreach (var chunk in chunksToDelete)
            {
                var smbChunkFilePath = $@"{chunk.DeviceId}\{VideoChunkStorageUtilities.GetChunkDayFolder(chunk.FileName)}\{chunk.FileName}";

                session.DeleteFile(smbChunkFilePath);
            }

            var deletionCount = await _videoChunkReplicationRepository.MarkReplicatedChunksAsDeletedAsync(target.Id, chunksToDelete.Select(x => x.Id).ToList());

            _logger.LogInformation("Deleted and marked {Count} expired video chunks from {SmbShare}", deletionCount, target.SharePath);
        }

        private async Task DeleteDbVideoChunkRecordsWithNoFootagePresentAnywhereAsync()
        {
            var deletedRecordsCount = await _videoChunkRepository.DeleteChunksWhichAreNotAvailbileInAnyStorageLocationAsync();

            if (deletedRecordsCount < 0)
            {
                return;
            }

            _logger.LogInformation("Deleted {deletedRecordsCount} video chunkl db records, since the footage associated with them, was already deleted from all sources", deletedRecordsCount);
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
