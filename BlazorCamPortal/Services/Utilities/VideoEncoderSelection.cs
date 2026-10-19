using CamPortal.Contracts.Enums;

namespace CamPortal.Core.Utilities
{
    public class VideoEncoderSelection
    {
        public const string NvidiaCodecName = "h264_nvenc";
        public const string IntelQuickSyncCodecName = "h264_qsv";
        public const string AmdAmfCodecName = "h264_amf";
        public const string VaapiCodecName = "h264_vaapi";
        public const string CpuCodecName = "libx264";

        public VideoHardwareEncoder Encoder { get; init; }

        public required string CodecName { get; init; }

        public string? VaapiDevicePath { get; init; }

        public string DeviceArguments => CodecName == VaapiCodecName
            ? $"-init_hw_device vaapi=gpu:{VaapiDevicePath},connection_type=drm -filter_hw_device gpu "
            : string.Empty;

        public string UploadFilter => CodecName == VaapiCodecName
            ? ",format=nv12,hwupload"
            : string.Empty;

        public string PixelFormatArguments => CodecName switch
        {
            VaapiCodecName => string.Empty,
            IntelQuickSyncCodecName => "-pix_fmt nv12 ",
            _ => "-pix_fmt yuv420p "
        };

        public override string ToString()
        {
            return string.IsNullOrEmpty(VaapiDevicePath)
                ? $"{Encoder} ({CodecName})"
                : $"{Encoder} ({CodecName} on {VaapiDevicePath})";
        }
    }
}
