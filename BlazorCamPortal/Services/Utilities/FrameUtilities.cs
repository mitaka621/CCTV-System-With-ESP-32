using Microsoft.Extensions.Configuration;

namespace BlazorCamPortal.Core.Utilities
{
    public static class FrameUtilities
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
    }
}
