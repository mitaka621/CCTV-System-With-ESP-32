using BlazorCamPortal.Contracts.Abstractions.Services;

namespace BlazorCamPortal.Core.Services
{
    public class CameraFramesManagerService : ICameraFramesManagerService
    {
        private readonly Dictionary<Guid, byte[]> _cameraFrames = new();

        public event Func<Guid, byte[], Task>? FrameAdded;

        public void AddFrame(Guid cameraId, byte[] frame)
        {
            if (cameraId == Guid.Empty)
                throw new ArgumentException("Camera ID cannot be empty", nameof(cameraId));
            if (frame == null || frame.Length == 0)
                throw new ArgumentException("Frame data cannot be null or empty", nameof(frame));

            _cameraFrames[cameraId] = frame;

            var handlers = FrameAdded;
            if (handlers != null)
            {
                foreach (Func<Guid, byte[], Task> handler in handlers.GetInvocationList())
                {
                    _ = handler.Invoke(cameraId, frame);
                }
            }
        }
    }
}
