using Homewatch.Contracts.Enums;
using SixLabors.ImageSharp.Processing;

namespace Homewatch.Contracts.Dtos.CameraConfigurationDtos
{
    public class CameraStreamingConfigurationDto
    {
        public Guid DeviceId { get; set; }

        public float FrameRotation { get; set; } = 0;

        public float ZoomFactor { get; set; } = 1;

        public int ZoomStartX { get; set; } = 0;

        public int ZoomStartY { get; set; } = 0;

        public float Brightness { get; set; } = 1;

        public float Contrast { get; set; } = 1;

        public FlipMode FlipMode { get; set; } = FlipMode.None;

        public CameraAspectRatios CameraAspectRatio { get; set; } = CameraAspectRatios.Original;

        public CameraSensorResolutions SensorResolution { get; set; } = CameraSensorResolutions.Resolution2048x1536;
    }
}
