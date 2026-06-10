using CamPortal.Contracts.Dtos.CameraFrameDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using System.Threading.Channels;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ICameraFramesManagerService
    {
        ChannelReader<(DeviceStreamingHandshakeDto, byte[])> RawFramesChannelReader { get; }

        public event Func<Guid, byte[], Task>? FrameProcessed;

        public event Action<Guid>? ChannelOpened;

        public event Action<Guid>? ChannelClosed;

        void AddFrame(DeviceStreamingHandshakeDto device, byte[] frame);

        void CloseProcessedFramesCameraChannel(Guid cameraId);

        CameraFrameDto StampFrame(byte[] frame, DeviceStreamingHandshakeDto camera);

        Channel<byte[]> GetOrCreateProcessedFramesCameraChannel(Guid cameraId);

        void PublishProcessedFrame(Guid cameraId, CameraFrameDto dto);

        void PublishPlaceholderToViewers(Guid cameraId);

        ChannelReader<byte[]> SubscribeViewer(Guid cameraId, Guid viewerId, bool isOriginalResolution);

        void UnsubscribeViewer(Guid cameraId, Guid viewerId, bool isOriginalResolution);
    }
}
