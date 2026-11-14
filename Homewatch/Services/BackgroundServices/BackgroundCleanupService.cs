using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Abstractions.Services;
using Homewatch.Contracts.Dtos.RemoteStorageDtos;
using Homewatch.Contracts.Dtos.SystemSettingsDtos;
using Homewatch.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Homewatch.Core.BackgroundServices
{
    public class BackgroundCleanupService : BackgroundService
    {
        private readonly IExportedVideoRepository _exportedVideoRepository;
        private readonly IVideoChunkRepository _videoChunkRepository;
        private readonly ISystemSettingsService _systemSettingsService;
        private readonly IVideoExportNotifier _videoExportNotifier;
        private readonly IStorageLocationService _storageLocationService;
        private readonly IRemoteStorageTargetRepository _remoteStorageTargetRepository;
        private readonly ISmbConnectionService _smbConnectionService;
        private readonly IVideoChunkReplicationRepository _videoChunkReplicationRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly ILogger<BackgroundCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval;

        public BackgroundCleanupService(
            IExportedVideoRepository exportedVideoRepository,
            IVideoChunkRepository videoChunkRepository,
            ISystemSettingsService systemSettingsService,
            IVideoExportNotifier videoExportNotifier,
            IStorageLocationService storageLocationService,
            IRemoteStorageTargetRepository remoteStorageTargetRepository,
            ISmbConnectionService smbConnectionService,
            IVideoChunkReplicationRepository videoChunkReplicationRepository,
            INotificationRepository notificationRepository,
            IConfiguration configuration,
            ILogger<BackgroundCleanupService> logger)
        {
            _exportedVideoRepository = exportedVideoRepository;
            _videoChunkRepository = videoChunkRepository;
            _systemSettingsService = systemSettingsService;
            _videoExportNotifier = videoExportNotifier;
            _storageLocationService = storageLocationService;
            _remoteStorageTargetRepository = remoteStorageTargetRepository;
            _smbConnectionService = smbConnectionService;
            _videoChunkReplicationRepository = videoChunkReplicationRepository;
            _notificationRepository = notificationRepository;
            _logger = logger;

            _cleanupInterval = TimeSpan.FromMinutes(int.Parse(
                configuration.GetSection("BackgroundCleanupConfig")["CheckForCleanupIntervalInMinutes"]
                ?? throw new ArgumentNullException("CheckForCleanupIntervalInMinutes not configured")));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_cleanupInterval);

            do
            {
                try
                {
                    _logger.LogInformation("Background cleanup started.");

                    await RunCleanupAsync();

                    _logger.LogInformation("Background cleanup finished.");
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Background cleanup run failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task RunCleanupAsync()
        {
            var settings = await _systemSettingsService.GetSystemSettingsAsync();

            await RunStepAsync("expired encoded videos", () => CleanupExpiredExportedVideosAsync(settings));
            await RunStepAsync("expired local camera chunks", () => CleanupExpiredCameraChunksFromLocalStorageAsync(settings));
            await RunStepAsync("expired remote camera chunks", CleanupExpiredCameraChunksFromAllRemoteStoragesAsync);
            await RunStepAsync("video chunk records without footage", DeleteDbVideoChunkRecordsWithNoFootagePresentAnywhereAsync);
            await RunStepAsync("dismissed notifications", () => CleanupDismissedNotificationsAsync(settings));
        }

        private async Task RunStepAsync(string stepName, Func<Task> step)
        {
            try
            {
                _logger.LogInformation("Background cleanup of {StepName} started.", stepName);
                await step();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Background cleanup of {StepName} failed.", stepName);
            }
        }

        private async Task CleanupExpiredExportedVideosAsync(SystemSettingsDto settings)
        {
            var retention = settings.EncodedVideoRetention.ToTimeSpan();

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

        private async Task CleanupExpiredCameraChunksFromLocalStorageAsync(SystemSettingsDto settings)
        {
            var retention = settings.CameraChunkRetention.ToTimeSpan();

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

        private async Task CleanupExpiredCameraChunksFromAllRemoteStoragesAsync()
        {
            var enabledTargets = await _remoteStorageTargetRepository.GetEnabledAsync();

            foreach (var remoteTarget in enabledTargets.Values)
            {
                await RunStepAsync($"expired camera chunks on {remoteTarget.SharePath}", () => CleanupExpiredCameraChunksFromRemoteStorageAsync(remoteTarget));
            }
        }

        private async Task CleanupExpiredCameraChunksFromRemoteStorageAsync(RemoteStorageTargetDto target)
        {
            var retention = target.Retention.ToTimeSpan();

            if (retention == null)
            {
                return;
            }

            using var session = _smbConnectionService.OpenSession(target);

            if (!session.IsConnected)
            {
                _logger.LogError("Failed to connect to {SmbShare}", target.SharePath);
                return;
            }

            var cutoffUtc = DateTime.UtcNow - retention.Value;

            var chunksToDelete = await _videoChunkReplicationRepository.GetAllExpiredReplicatedChunksForTargetAsync(target.Id, cutoffUtc);

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

        private async Task CleanupDismissedNotificationsAsync(SystemSettingsDto settings)
        {
            var retention = settings.DismissedNotificationRetention.ToTimeSpan();

            if (retention == null)
            {
                return;
            }

            var deletedCount = await _notificationRepository.DeleteDismissedNotificationsOlderThanAsync(DateTime.UtcNow - retention.Value);

            if (deletedCount > 0)
            {
                _logger.LogInformation("Deleted {Count} dismissed notifications older than {Retention}.", deletedCount, settings.DismissedNotificationRetention);
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
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to delete file {Path} during background cleanup.", path);
            }
        }
    }
}
