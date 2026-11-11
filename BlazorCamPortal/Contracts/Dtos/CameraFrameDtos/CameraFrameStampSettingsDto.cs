using CamPortal.Contracts.Enums;
using SixLabors.ImageSharp.Processing;

namespace CamPortal.Contracts.Dtos.CameraFrameDtos
{
    public record CameraFrameStampSettingsDto
    {
        public Guid CameraId { get; init; }

        public required string CameraName { get; init; }

        public int FrameWidth { get; init; }

        public int FrameHeight { get; init; }

        public float FrameRotation { get; init; }

        public float ZoomFactor { get; init; }

        public int ZoomStartX { get; init; }

        public int ZoomStartY { get; init; }

        public float Brightness { get; init; }

        public float Contrast { get; init; }

        public FlipMode FlipMode { get; init; }

        public CameraAspectRatios CameraAspectRatio { get; init; }

        public int UtcOffsetSeconds { get; init; }
    }
}
