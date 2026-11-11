using CamPortal.Contracts.Dtos.DeviceDtos;
using System.Threading.Channels;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraFramesManagerService
    {
        ChannelReader<(DeviceStreamingHandshakeWithCameraConfigDto, byte[])> RawFramesChannelReader { get; }

        public event Func<Guid, byte[], Task>? FrameProcessed;

        public event Action<Guid>? ChannelOpened;

        public event Action<Guid>? ChannelClosed;

        void AddFrame(DeviceStreamingHandshakeWithCameraConfigDto device, byte[] frame);

        void CloseProcessedFramesCameraChannel(Guid cameraId);

        Channel<byte[]> GetOrCreateProcessedFramesCameraChannel(Guid cameraId);

        void PublishOriginalResolutionFrame(Guid cameraId, byte[] frame);

        void PublishReducedResolutionFrame(Guid cameraId, byte[] frame);

        void PublishPlaceholderToViewers(Guid cameraId);

        ChannelReader<byte[]> SubscribeViewer(Guid cameraId, Guid viewerId, bool isOriginalResolution);

        void UnsubscribeViewer(Guid cameraId, Guid viewerId, bool isOriginalResolution);
    }
}
