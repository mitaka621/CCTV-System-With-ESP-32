using CamPortal.Contracts.Abstractions.Services;
using System.Collections.Concurrent;

namespace CamPortal.Core.Services.Devices
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
            return _byCameraId.TryRemove(cameraId, out var cts) && Disconnect(cameraId, cts);
        }

        //if a camera loses connection there is a 15 sec timeout and then it is marked as disconnected.
        //however if the camera connects before this 15 sec period then its new connection is canceld after 15 seconds elaps.
        //This is why this method exisits so after 15 seconds the connection is disconnected only if there isnt a new session token by a new connection.
        public bool TryDisconnect(Guid cameraId, CancellationToken sessionToken)
        {
            return _byCameraId.TryGetValue(cameraId, out var cts)
                && cts.Token == sessionToken
                && _byCameraId.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(cameraId, cts))
                && Disconnect(cameraId, cts);
        }

        private bool Disconnect(Guid cameraId, CancellationTokenSource cts)
        {
            cts.Cancel();
            _cameraFramesManagerService.PublishPlaceholderToViewers(cameraId);
            return true;
        }

        public int TotalActiveCameraConnevtions()
        {
            return _byCameraId.Count;
        }
    }
}
