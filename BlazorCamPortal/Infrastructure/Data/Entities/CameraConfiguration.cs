using SixLabors.ImageSharp.Processing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CamPortal.Infrastructure.Data.Entities
{
    public class CameraConfiguration
    {
        [Key]
        public Guid DeviceId { get; set; }
        [ForeignKey(nameof(DeviceId))]
        public Device Device { get; set; } = null!;

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
