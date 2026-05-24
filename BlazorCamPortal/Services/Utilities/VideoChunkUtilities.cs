using System.Diagnostics;
using System.Runtime.InteropServices;
using CamPortal.Contracts.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CamPortal.Core.Utilities
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

        public static string GetCodecName(VideoHardwareEncoder encoder)
        {
            return encoder switch
            {
                VideoHardwareEncoder.Nvidia => "h264_nvenc",
                VideoHardwareEncoder.Intel => "h264_qsv",
                VideoHardwareEncoder.Amd => "h264_amf",
                _ => "libx264"
            };
        }

        public static VideoHardwareEncoder ResolveHardwareEncoder(VideoHardwareEncoder requested, ILogger logger)
        {
            if (requested == VideoHardwareEncoder.Cpu)
            {
                logger.LogInformation("Video encoder: CPU (libx264) selected by configuration");
                return VideoHardwareEncoder.Cpu;
            }

            if (requested == VideoHardwareEncoder.Auto)
            {
                foreach (var candidate in new[] { VideoHardwareEncoder.Nvidia, VideoHardwareEncoder.Intel, VideoHardwareEncoder.Amd })
                {
                    if (TestEncoder(GetCodecName(candidate), logger))
                    {
                        logger.LogInformation("Video encoder: {Encoder} ({Codec}) auto-detected", candidate, GetCodecName(candidate));
                        return candidate;
                    }
                }

                logger.LogInformation("Video encoder: no hardware encoder available, using CPU (libx264)");
                return VideoHardwareEncoder.Cpu;
            }

            var codec = GetCodecName(requested);
            if (TestEncoder(codec, logger))
            {
                logger.LogInformation("Video encoder: {Encoder} ({Codec}) selected by configuration", requested, codec);
                return requested;
            }

            logger.LogWarning("Video encoder: requested {Encoder} ({Codec}) is not available on this host, falling back to CPU (libx264)", requested, codec);
            return VideoHardwareEncoder.Cpu;
        }

        private static bool TestEncoder(string codecName, ILogger logger)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = GetFfmpegPath(),
                        Arguments = $"-hide_banner -nostdin -f lavfi -i color=c=black:s=320x240:r=30:d=1 -c:v {codecName} -pix_fmt yuv420p -f null -",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var stderrTask = process.StandardError.ReadToEndAsync();
                var stdoutTask = process.StandardOutput.ReadToEndAsync();

                if (!process.WaitForExit(15000))
                {
                    try { process.Kill(true); } catch { }
                    logger.LogDebug("Encoder probe for {Codec} timed out after 15s", codecName);
                    return false;
                }

                string stderr = string.Empty;
                try { stderr = stderrTask.GetAwaiter().GetResult(); } catch { }
                try { stdoutTask.GetAwaiter().GetResult(); } catch { }

                if (process.ExitCode == 0)
                {
                    return true;
                }

                logger.LogDebug(
                    "Encoder probe for {Codec} failed (exit {ExitCode}): {Stderr}",
                    codecName,
                    process.ExitCode,
                    string.IsNullOrWhiteSpace(stderr) ? "(no stderr)" : stderr.Trim());
                return false;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Encoder probe for {Codec} threw", codecName);
                return false;
            }
        }
    }
}
