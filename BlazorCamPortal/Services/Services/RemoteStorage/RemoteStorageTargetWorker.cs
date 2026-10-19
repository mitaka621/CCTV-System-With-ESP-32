using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.RemoteStorageDtos;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace CamPortal.Core.Services.RemoteStorage
{
    public class RemoteStorageTargetWorker
    {
        private const string _partialFileExtension = ".partial";

        private readonly Channel<VideoChunkReplicationItemDto> _channel = Channel.CreateUnbounded<VideoChunkReplicationItemDto>();
        private readonly HashSet<string> _createdDirectories = new(StringComparer.OrdinalIgnoreCase);

        private readonly IRemoteStorageTargetRepository _remoteStorageTargetRepository;
        private readonly IVideoChunkReplicationRepository _videoChunkReplicationRepository;
        private readonly ISmbConnectionService _smbConnectionService;
        private readonly IStorageLocationService _storageLocationService;
        private readonly ILogger<RemoteStorageTargetWorker> _logger;
        private readonly TimeSpan _retryDelay;
        private readonly TimeSpan? _retention;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private Task _processingTask = Task.CompletedTask;
        private ISmbShareSession? _session;
        private bool _isFailing;

        public RemoteStorageTargetWorker(
            RemoteStorageTargetDto target,
            IRemoteStorageTargetRepository remoteStorageTargetRepository,
            ISmbConnectionService smbConnectionService,
            IStorageLocationService storageLocationService,
            ILogger<RemoteStorageTargetWorker> logger,
            TimeSpan retryDelay,
            CancellationToken stoppingToken,
            IVideoChunkReplicationRepository videoChunkReplicationRepository)
        {
            Target = target;
            _remoteStorageTargetRepository = remoteStorageTargetRepository;
            _smbConnectionService = smbConnectionService;
            _storageLocationService = storageLocationService;
            _logger = logger;
            _retryDelay = retryDelay;
            _retention = target.Retention.ToTimeSpan();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _videoChunkReplicationRepository = videoChunkReplicationRepository;
        }

        public RemoteStorageTargetDto Target { get; }

        public void Start()
        {
            _processingTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
        }

        public void Enqueue(VideoChunkReplicationItemDto item)
        {
            _channel.Writer.TryWrite(item);
        }

        public async Task StopAsync(TimeSpan timeout)
        {
            _channel.Writer.TryComplete();
            await _cancellationTokenSource.CancelAsync();

            try
            {
                await _processingTask.WaitAsync(timeout);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning(
                    "Video sync to {SharePath} did not stop within {Timeout} seconds and is left to finish in the background.",
                    Target.SharePath,
                    timeout.TotalSeconds);

                return;
            }
            catch (OperationCanceledException)
            {
            }

            _cancellationTokenSource.Dispose();
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await foreach (var replicationItem in _channel.Reader.ReadAllAsync(cancellationToken))
                    {
                        await ReplicateVideoChunkAsync(replicationItem, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Video sync to {SharePath} stopped unexpectedly.", Target.SharePath);
            }
            finally
            {
                CloseSession();
            }
        }

        private async Task ReplicateVideoChunkAsync(VideoChunkReplicationItemDto item, CancellationToken cancellationToken)
        {
            if (!_storageLocationService.TryGetCameraChunkFullPath(item.DeviceId.ToString(), item.FileName, out var localChunkPath))
            {
                return;
            }

            while (File.Exists(localChunkPath))
            {
                try
                {
                    if (await _videoChunkReplicationRepository.IsVideoChunkReplicatedAsync(Target.Id, item.VideoChunkId))
                    {
                        return;
                    }

                    if (!IsExpiredOnTarget(item.ChunkEndTime))
                    {
                        CopyVideoChunk(localChunkPath, item);
                    }

                    await _videoChunkReplicationRepository.MarkVideoChunkReplicatedAsync(Target.Id, item.VideoChunkId);

                    if (_isFailing)
                    {
                        _isFailing = false;
                        _logger.LogInformation("Video sync to {SharePath} recovered.", Target.SharePath);
                    }

                    return;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    CloseSession();

                    if (!_isFailing)
                    {
                        _isFailing = true;
                        _logger.LogWarning(
                            exception,
                            "Copying video chunk {FileName} to {SharePath} failed. Retrying every {RetryDelay} seconds.",
                            item.FileName,
                            Target.SharePath,
                            _retryDelay.TotalSeconds);
                    }

                    await Task.Delay(_retryDelay, cancellationToken);
                }
            }
        }

        private void CopyVideoChunk(string localChunkPath, VideoChunkReplicationItemDto item)
        {
            var session = GetSession();

            var dayFolderName = Path.GetFileName(Path.GetDirectoryName(localChunkPath)) ?? string.Empty;
            var remoteDirectory = $@"{item.DeviceId}\{dayFolderName}";
            var remoteChunkPath = $@"{remoteDirectory}\{item.FileName}";
            var partialRemoteChunkPath = remoteChunkPath + _partialFileExtension;

            if (_createdDirectories.Add(remoteDirectory))
            {
                session.CreateDirectory(remoteDirectory);
            }

            using (var source = new FileStream(localChunkPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                session.UploadFile(source, partialRemoteChunkPath);
            }

            session.RenameFile(partialRemoteChunkPath, remoteChunkPath);
        }

        private bool IsExpiredOnTarget(DateTime chunkEndTimeUtc)
        {
            return _retention != null && chunkEndTimeUtc < DateTime.UtcNow - _retention.Value;
        }

        private ISmbShareSession GetSession()
        {
            if (_session is { IsConnected: true })
            {
                return _session;
            }

            CloseSession();

            _session = _smbConnectionService.OpenSession(Target);

            return _session;
        }

        private void CloseSession()
        {
            _session?.Dispose();
            _session = null;
            _createdDirectories.Clear();
        }
    }
}
