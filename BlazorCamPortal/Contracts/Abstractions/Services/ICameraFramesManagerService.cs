using System.Collections.Concurrent;
using System.Threading.Channels;

namespace BlazorCamPortal.Contracts.Abstractions.Services
{
    public interface ICameraFramesManagerService
    {
        ConcurrentDictionary<Guid, Channel<byte[]>> GetCameraChannels { get; }

        public event Func<Guid, byte[], Task>? FrameAdded;

        public event Action<Guid>? ChannelOpened;

        public event Action<Guid>? ChannelClosed;

        void AddFrame(Guid cameraId, byte[] frame);

        void CloseChannel(Guid cameraId);

        Task InitializeAsync(ICameraService _cameraService);

        Channel<byte[]> GetOrCreateChannel(Guid cameraId);
    }
}
