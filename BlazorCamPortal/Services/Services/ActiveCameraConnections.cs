using CamPortal.Contracts.Abstractions.Services;
using System.Collections.Concurrent;

namespace CamPortal.Core.Services
{
    public class ActiveCameraConnections : IActiveCameraConnections
    {
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _byCameraId = new();

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

        public bool TryDisconnect(Guid cameraId)
        {
            if (_byCameraId.TryRemove(cameraId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                return true;
            }
            return false;
        }
    }
}
