using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.RemoteStorageDtos;
using CamPortal.Core.Services.RemoteStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace CamPortal.Core.BackgroundServices
{
    public class RemoteStorageReplicationService : BackgroundService, IChunkReplicationQueue
    {
        private readonly IRemoteStorageTargetRepository _remoteStorageTargetRepository;
        private readonly IVideoChunkRepository _videoChunkRepository;
        private readonly ISmbConnectionService _smbConnectionService;
        private readonly IStorageLocationService _storageLocationService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RemoteStorageReplicationService> _logger;
        private readonly IVideoChunkReplicationRepository _videoChunkReplicationRepository;
        private readonly int _settingsCheckIntervalInChunks;
        private readonly TimeSpan _retryDelay;
        private readonly TimeSpan _workerStopTimeout = TimeSpan.FromSeconds(5);
        private readonly Dictionary<Guid, RemoteStorageTargetWorker> _workers = new();

        public RemoteStorageReplicationService(
            IRemoteStorageTargetRepository remoteStorageTargetRepository,
            ISmbConnectionService smbConnectionService,
            IStorageLocationService storageLocationService,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            ILogger<RemoteStorageReplicationService> logger,
            IVideoChunkRepository videoChunkRepository,
            IVideoChunkReplicationRepository videoChunkReplicationRepository)
        {
            _remoteStorageTargetRepository = remoteStorageTargetRepository;
            _smbConnectionService = smbConnectionService;
            _storageLocationService = storageLocationService;
            _loggerFactory = loggerFactory;
            _logger = logger;
            _videoChunkRepository = videoChunkRepository;
            _videoChunkReplicationRepository = videoChunkReplicationRepository;

            _settingsCheckIntervalInChunks = int.Parse(
                configuration.GetSection("RemoteStorageConfig")["SettingsCheckIntervalInChunks"]
                ?? throw new ArgumentNullException("SettingsCheckIntervalInChunks not configured"));

            _retryDelay = TimeSpan.FromSeconds(int.Parse(
                configuration.GetSection("RemoteStorageConfig")["RetryDelayInSeconds"]
                ?? throw new ArgumentNullException("RetryDelayInSeconds not configured")));
        }

        private readonly Channel<VideoChunkReplicationItemDto> _channel = Channel.CreateUnbounded<VideoChunkReplicationItemDto>();

        public void Enqueue(VideoChunkReplicationItemDto item)
        {
            _channel.Writer.TryWrite(item);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RefreshTargetsAsync(stoppingToken);

            var savedChunksSinceLastCheck = 0;

            try
            {
                await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
                {
                    savedChunksSinceLastCheck++;

                    if (savedChunksSinceLastCheck >= _settingsCheckIntervalInChunks)
                    {
                        savedChunksSinceLastCheck = 0;
                        await RefreshTargetsAsync(stoppingToken);
                    }

                    foreach (var worker in _workers.Values)
                    {
                        worker.Enqueue(item);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            finally
            {
                foreach (var worker in _workers.Values)
                {
                    await worker.StopAsync(_workerStopTimeout);
                }

                _workers.Clear();
            }
        }

        private async Task RefreshTargetsAsync(CancellationToken stoppingToken)
        {
            Dictionary<Guid, RemoteStorageTargetDto> enabledTargets;

            try
            {
                enabledTargets = await _remoteStorageTargetRepository.GetEnabledAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to load remote storage settings. Keeping the current video sync targets.");
                return;
            }

            foreach (var worker in _workers.Values.ToList())
            {
                if (enabledTargets.TryGetValue(worker.Target.Id, out var storedTarget) && !HasTargetChanged(worker.Target, storedTarget))
                {
                    continue;
                }

                await StopWorkerAsync(worker);
            }

            foreach (var target in enabledTargets.Values.Where(x => !_workers.ContainsKey(x.Id)))
            {
                await StartWorkerAsync(target, stoppingToken);
            }
        }

        private async Task StartWorkerAsync(RemoteStorageTargetDto target, CancellationToken stoppingToken)
        {
            var worker = new RemoteStorageTargetWorker(
                target,
                _remoteStorageTargetRepository,
                _smbConnectionService,
                _storageLocationService,
                _loggerFactory.CreateLogger<RemoteStorageTargetWorker>(),
                _retryDelay,
                stoppingToken,
                _videoChunkReplicationRepository);

            _workers[target.Id] = worker;
            worker.Start();

            try
            {
                var unreplicatedVideoChunks = await _videoChunkRepository.GetUnreplicatedVideoChunksAsync(target.Id);

                foreach (var unreplicatedVideoChunk in unreplicatedVideoChunks)
                {
                    worker.Enqueue(unreplicatedVideoChunk);
                }

                _logger.LogInformation(
                    "Started video sync to {SharePath} with {Count} pending chunks.",
                    target.SharePath,
                    unreplicatedVideoChunks.Count);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to load pending video chunks for {SharePath}. Retrying on the next settings check.", target.SharePath);

                await StopWorkerAsync(worker);
            }
        }

        private async Task StopWorkerAsync(RemoteStorageTargetWorker worker)
        {
            await worker.StopAsync(_workerStopTimeout);

            _workers.Remove(worker.Target.Id);

            _logger.LogInformation("Stopped video sync to {SharePath}.", worker.Target.SharePath);
        }

        private static bool HasTargetChanged(RemoteStorageTargetDto runningTarget, RemoteStorageTargetDto storedTarget)
        {
            return !string.Equals(runningTarget.SharePath, storedTarget.SharePath, StringComparison.OrdinalIgnoreCase)
                || runningTarget.Username != storedTarget.Username
                || runningTarget.ProtectedPassword != storedTarget.ProtectedPassword
                || runningTarget.Retention != storedTarget.Retention;
        }
    }
}
