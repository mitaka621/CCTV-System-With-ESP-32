using System.Threading.Channels;

namespace CamPortal.Contracts.Dtos.CameraFrameDtos
{
    public class CameraFramesForViewerDto
    {
        public ChannelWriter<byte[]>? OriginalFrameResolutionChannel { get; set; }

        public ChannelWriter<byte[]>? ReducedFrameResolutionChannel { get; set; }
    }
}
