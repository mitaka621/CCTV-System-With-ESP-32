using System.Threading.Channels;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraFramesManagerService
    {
        ChannelReader<(Guid, byte[])> RawFramesChannelReader { get; }

        public event Func<Guid, byte[], Task>? FrameProcessed;

        public event Action<Guid>? ChannelOpened;

        public event Action<Guid>? ChannelClosed;

        void AddFrame(Guid cameraId, byte[] frame);

        void CloseProcessedFramesCameraChannel(Guid cameraId);

        Task InitializeAsync(ICameraService _cameraService);

        byte[] StampFrame(byte[] frame);

        Channel<byte[]> GetOrCreateProcessedFramesCameraChannel(Guid cameraId);

        void PublishProcessedFrame(Guid cameraId, byte[] frame);

        ChannelReader<byte[]> SubscribeViewer(Guid cameraId, Guid viewerId);

        void UnsubscribeViewer(Guid cameraId, Guid viewerId);
    }
}
