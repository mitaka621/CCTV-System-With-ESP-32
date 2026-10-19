using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using CamPortal.Contracts.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CamPortal.Core.Utilities
{
    public static class VideoChunkUtilities
    {
        private const string LinuxRenderDevicesDirectory = "/dev/dri";
        private const string AmdVendorId = "0x1002";
        private const string IntelVendorId = "0x8086";

        private static int _isFfmpegExecutablePermissionChecked;

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
            {
                var linuxFfmpegPath = Path.Combine(basePath, "ffmpeg", "linux-x64", "ffmpeg");
                EnsureFfmpegIsExecutable(linuxFfmpegPath);
                return linuxFfmpegPath;
            }

            throw new PlatformNotSupportedException("Unsupported OS for FFmpeg");
        }

        private static void EnsureFfmpegIsExecutable(string ffmpegPath)
        {
            if (!OperatingSystem.IsLinux()
                || Interlocked.Exchange(ref _isFfmpegExecutablePermissionChecked, 1) == 1
                || !File.Exists(ffmpegPath))
            {
                return;
            }

            try
            {
                var fileMode = File.GetUnixFileMode(ffmpegPath);

                if ((fileMode & UnixFileMode.UserExecute) == 0)
                {
                    File.SetUnixFileMode(ffmpegPath, fileMode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                }
            }
            catch (Exception)
            {
            }
        }

        public static bool TryGetChunkEndTimeFromFileName(string fileName, out DateTime chunkEndTimeUtc)
        {
            const string endTimeMarker = "__";
            const string fileNameDateFormat = "yyyy-MM-dd_HH-mm-ss";

            chunkEndTimeUtc = default;

            var markerIndex = fileName.LastIndexOf(endTimeMarker, StringComparison.Ordinal);
            var endTimeStart = markerIndex + endTimeMarker.Length;

            if (markerIndex < 0 || fileName.Length < endTimeStart + fileNameDateFormat.Length)
            {
                return false;
            }

            return DateTime.TryParseExact(
                fileName.Substring(endTimeStart, fileNameDateFormat.Length),
                fileNameDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out chunkEndTimeUtc);
        }

        public static bool TryParseChunkDayFolderName(string folderName, out DateTime dayUtc)
        {
            return DateTime.TryParseExact(
                folderName,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out dayUtc);
        }

        public static VideoEncoderSelection ResolveHardwareEncoder(VideoHardwareEncoder requested, ILogger logger)
        {
            var cpuSelection = new VideoEncoderSelection
            {
                Encoder = VideoHardwareEncoder.Cpu,
                CodecName = VideoEncoderSelection.CpuCodecName
            };

            if (requested == VideoHardwareEncoder.Cpu)
            {
                logger.LogInformation("Video encoder: CPU (libx264) selected by configuration");
                return cpuSelection;
            }

            var candidates = GetHardwareEncoderCandidates()
                .Where(x => requested == VideoHardwareEncoder.Auto || x.Encoder == requested);

            foreach (var candidate in candidates)
            {
                if (TestEncoder(candidate, logger))
                {
                    logger.LogInformation(
                        "Video encoder: {Encoder} {Source}",
                        candidate,
                        requested == VideoHardwareEncoder.Auto ? "auto-detected" : "selected by configuration");

                    return candidate;
                }
            }

            if (requested == VideoHardwareEncoder.Auto)
            {
                logger.LogInformation("Video encoder: no hardware encoder available, using CPU (libx264)");
            }
            else
            {
                logger.LogWarning("Video encoder: requested {Encoder} is not available on this host, falling back to CPU (libx264)", requested);
            }

            return cpuSelection;
        }

        private static IEnumerable<VideoEncoderSelection> GetHardwareEncoderCandidates()
        {
            yield return new VideoEncoderSelection { Encoder = VideoHardwareEncoder.Nvidia, CodecName = VideoEncoderSelection.NvidiaCodecName };

            if (!OperatingSystem.IsLinux())
            {
                yield return new VideoEncoderSelection { Encoder = VideoHardwareEncoder.Intel, CodecName = VideoEncoderSelection.IntelQuickSyncCodecName };
                yield return new VideoEncoderSelection { Encoder = VideoHardwareEncoder.Amd, CodecName = VideoEncoderSelection.AmdAmfCodecName };
                yield break;
            }

            foreach (var renderDevicePath in GetLinuxRenderDevicePaths())
            {
                switch (GetLinuxRenderDeviceVendorId(renderDevicePath))
                {
                    case IntelVendorId:
                        yield return new VideoEncoderSelection { Encoder = VideoHardwareEncoder.Intel, CodecName = VideoEncoderSelection.IntelQuickSyncCodecName };
                        yield return new VideoEncoderSelection { Encoder = VideoHardwareEncoder.Intel, CodecName = VideoEncoderSelection.VaapiCodecName, VaapiDevicePath = renderDevicePath };
                        break;

                    case AmdVendorId:
                        yield return new VideoEncoderSelection { Encoder = VideoHardwareEncoder.Amd, CodecName = VideoEncoderSelection.VaapiCodecName, VaapiDevicePath = renderDevicePath };
                        yield return new VideoEncoderSelection { Encoder = VideoHardwareEncoder.Amd, CodecName = VideoEncoderSelection.AmdAmfCodecName };
                        break;
                }
            }
        }

        private static IEnumerable<string> GetLinuxRenderDevicePaths()
        {
            if (!Directory.Exists(LinuxRenderDevicesDirectory))
            {
                return [];
            }

            return Directory.GetFiles(LinuxRenderDevicesDirectory, "renderD*").Order(StringComparer.Ordinal);
        }

        private static string? GetLinuxRenderDeviceVendorId(string renderDevicePath)
        {
            try
            {
                var vendorFilePath = Path.Combine("/sys/class/drm", Path.GetFileName(renderDevicePath), "device", "vendor");

                return File.Exists(vendorFilePath)
                    ? File.ReadAllText(vendorFilePath).Trim().ToLowerInvariant()
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool TestEncoder(VideoEncoderSelection candidate, ILogger logger)
        {
            var codecName = candidate.CodecName;

            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = GetFfmpegPath(),
                        Arguments = $"-hide_banner -nostdin {candidate.DeviceArguments}-f lavfi -i color=c=black:s=320x240:r=30:d=1 -vf scale=320:240{candidate.UploadFilter} -c:v {codecName} {candidate.PixelFormatArguments}-f null -",
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
