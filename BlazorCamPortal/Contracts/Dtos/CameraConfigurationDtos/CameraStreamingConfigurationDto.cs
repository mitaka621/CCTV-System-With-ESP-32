using SixLabors.ImageSharp.Processing;

namespace CamPortal.Contracts.Dtos.CameraConfigurationDtos
{
    public class CameraStreamingConfigurationDto
    {
        public float FrameRotation { get; set; } = 0;

        public float ZoomFactor { get; set; } = 1;

        public int ZoomStartX { get; set; } = 0;

        public int ZoomStartY { get; set; } = 0;

        public float Brightness { get; set; } = 1;

        public float Contrast { get; set; } = 1;

        public FlipMode FlipMode { get; set; } = FlipMode.None;

        public float SharpenFactor { get; set; } = 0;
    }
}
