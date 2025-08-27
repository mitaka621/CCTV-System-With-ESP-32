using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace BlazorCamPortal.Core.Utilities
{
    public static class VideoChunkUtilities
    {
        public static byte[] GetDefaultFrame(IConfiguration configuration)
        {
            var defaultFramePath = configuration.GetSection("ESPCamera")["DefaultFramePathOnNoImageTransmission"];

            if (!string.IsNullOrEmpty(defaultFramePath))
            {
                var workingDirectory = Directory.GetCurrentDirectory();
                var absolutePath = Path.Combine(workingDirectory, defaultFramePath);

                if (File.Exists(absolutePath))
                {
                    return File.ReadAllBytes(absolutePath);
                }
            }

            return Array.Empty<byte>();
        }

        public static string GetFfmpegPath()
        {
            string basePath = AppContext.BaseDirectory;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine(basePath, "ffmpeg", "win-x64", "ffmpeg.exe");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return Path.Combine(basePath, "ffmpeg", "linux-x64", "ffmpeg");

            throw new PlatformNotSupportedException("Unsupported OS for FFmpeg");
        }
    }
}
