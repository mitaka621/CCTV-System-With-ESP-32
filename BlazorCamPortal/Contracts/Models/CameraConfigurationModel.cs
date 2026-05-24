using MudBlazor;
using SixLabors.ImageSharp.Processing;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Models
{
    public class CameraConfigurationModel
    {

        [StringLength(100, MinimumLength = 1, ErrorMessage = "Camera name must be between 1 and 100 characters.")]
        [Label("Camera Name")]
        public required string CameraName { get; set; } = string.Empty;

        public Guid DeviceId { get; set; }

        [Range(-180, 180, ErrorMessage = "Frame rotation must be between -180 and 180 degrees.")]
        public float FrameRotation { get; set; } = 0;

        [Range(1, 2, ErrorMessage = "Zoom factor must be between 1 and 2.")]
        public float ZoomFactor { get; set; } = 1;

        [Range(0, int.MaxValue, ErrorMessage = "Zoom start X must be a non-negative integer.")]
        public int ZoomStartX { get; set; } = 0;

        [Range(0, int.MaxValue, ErrorMessage = "Zoom start Y must be a non-negative integer.")]
        public int ZoomStartY { get; set; } = 0;

        [Range(0, 2, ErrorMessage = "Brightness must be between 0 and 2.")]
        public float Brightness { get; set; } = 1;

        [Range(0, 2, ErrorMessage = "Contrast must be between 0 and 2.")]
        public float Contrast { get; set; } = 1;

        public FlipMode FlipMode { get; set; } = FlipMode.None;

        [Range(0, 2, ErrorMessage = "Sharpen factor must be between 0 and 2.")]
        public float SharpenFactor { get; set; } = 0;
    }
}
