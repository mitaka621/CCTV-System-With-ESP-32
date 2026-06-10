using CamPortal.Contracts.Abstractions.Services;
using System.Collections.Concurrent;

namespace CamPortal.Core.Services
{
    public class ActiveCameraConnections : IActiveCameraConnections
    {
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _byCameraId = new();
        private readonly ICameraFramesManagerService _cameraFramesManagerService;

        public ActiveCameraConnections(ICameraFramesManagerService cameraFramesManagerService)
        {
            _cameraFramesManagerService = cameraFramesManagerService;
        }

        public CancellationToken Register(Guid cameraId, CancellationToken linkedTo)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(linkedTo);
            _byCameraId.AddOrUpdate(cameraId,
                cts,
                (_, existing) =>
                {
                    existing.Cancel();
                    existing.Dispose();
                    return cts;
                });
            return cts.Token;
        }

        public bool IsCameraActive(Guid cameraId)
        {
            return _byCameraId.ContainsKey(cameraId);
        }

        public bool TryDisconnect(Guid cameraId)
        {
            if (_byCameraId.TryRemove(cameraId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _cameraFramesManagerService.PublishPlaceholderToViewers(cameraId);
                return true;
            }
            return false;
        }
    }
}
