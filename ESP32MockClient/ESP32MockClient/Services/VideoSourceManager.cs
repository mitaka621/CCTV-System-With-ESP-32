using ESP32MockClient.Configuration;
using System.Diagnostics;
using System.Globalization;

namespace ESP32MockClient.Services;

public class VideoSourceManager : IDisposable
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly List<string> _videoFiles = [];
    private readonly Random _random = new();

    private Process? _ffmpegProcess;
    private BinaryReader? _frameReader;
    private string? _currentVideo;
    private bool _disposed;

    public int VideoCount => _videoFiles.Count;
    public string? CurrentVideo => _currentVideo;

    public VideoSourceManager()
    {
        var ffmpegDir = Path.GetFullPath(MockClientConfiguration.FfmpegPath);
        _ffmpegPath = Path.Combine(ffmpegDir, "win-x64\\ffmpeg.exe");
        _ffprobePath = Path.Combine(ffmpegDir, "win-x64\\ffprobe.exe");
    }

    public bool Initialize()
    {
        if (!File.Exists(_ffmpegPath))
        {
            Console.WriteLine($"[VideoSource] FFmpeg not found at: {_ffmpegPath}");
            return false;
        }

        var footagePath = Path.GetFullPath(MockClientConfiguration.FootagePath);

        if (!Directory.Exists(footagePath))
        {
            Directory.CreateDirectory(footagePath);
            return false;
        }

        var extensions = new[] { "*.mp4", "*.avi", "*.mkv", "*.mov", "*.webm" };
        foreach (var ext in extensions)
        {
            _videoFiles.AddRange(Directory.GetFiles(footagePath, ext, SearchOption.AllDirectories));
        }

        Console.WriteLine($"[VideoSource] Found {_videoFiles.Count} video files");
        return _videoFiles.Count > 0;
    }

    public bool StartRandomVideo()
    {
        if (_videoFiles.Count == 0)
            return false;

        StopCurrentVideo();

        string selectedVideo;
        if (_videoFiles.Count == 1)
        {
            selectedVideo = _videoFiles[0];
        }
        else
        {
            do
            {
                selectedVideo = _videoFiles[_random.Next(_videoFiles.Count)];
            } while (selectedVideo == _currentVideo && _videoFiles.Count > 1);
        }

        _currentVideo = selectedVideo;

        var duration = GetVideoDuration(selectedVideo);
        double seekPosition = 0;

        if (duration > 0)
        {
            var maxSeek = duration * 0.7;
            seekPosition = _random.NextDouble() * maxSeek;
            Console.WriteLine($"[VideoSource] Playing: {Path.GetFileName(selectedVideo)} from {seekPosition:F1}s / {duration:F1}s");
        }
        else
        {
            Console.WriteLine($"[VideoSource] Playing: {Path.GetFileName(selectedVideo)} from start (no duration info)");
        }

        return StartFfmpeg(selectedVideo, seekPosition);
    }

    private double GetVideoDuration(string videoPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = $"-v error -show_entries format=duration -of csv=p=0 \"{videoPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return 0;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            if (double.TryParse(output, CultureInfo.InvariantCulture, out var duration))
                return duration;
        }
        catch { }

        return 0;
    }

    private bool StartFfmpeg(string videoPath, double seekPosition)
    {
        try
        {
            var fps = MockClientConfiguration.TargetFps;
            var resolution = MockClientConfiguration.Resolution;
            var resParts = resolution.Split('x');
            var width = resParts[0];
            var height = resParts[1];

            var filters = $"scale={width}:{height},fps={fps}";
            var args = $"-ss {seekPosition:F2} -i \"{videoPath}\" -vf \"{filters}\" -f image2pipe -vcodec mjpeg -q:v 5 -";

            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _ffmpegProcess = Process.Start(psi);
            if (_ffmpegProcess == null)
                return false;

            _frameReader = new BinaryReader(_ffmpegProcess.StandardOutput.BaseStream);
            _ = Task.Run(() => _ffmpegProcess.StandardError.ReadToEnd());

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VideoSource] FFmpeg start error: {ex.Message}");
            return false;
        }
    }

    public byte[]? GetNextFrame()
    {
        if (_frameReader == null || _ffmpegProcess == null)
            return null;

        try
        {
            return ReadJpegFrame();
        }
        catch
        {
            if (!_ffmpegProcess.HasExited)
                return null;

            Console.WriteLine("[VideoSource] Video ended, switching...");

            if (StartRandomVideo())
                return GetNextFrame();

            return null;
        }
    }

    private byte[]? ReadJpegFrame()
    {
        if (_frameReader == null)
            return null;

        var buffer = new List<byte>(50000);

        int b1 = -1, b2 = -1;
        while (true)
        {
            b1 = b2;
            b2 = _frameReader.BaseStream.ReadByte();

            if (b2 == -1)
                throw new EndOfStreamException();

            if (b1 == 0xFF && b2 == 0xD8)
            {
                buffer.Add(0xFF);
                buffer.Add(0xD8);
                break;
            }
        }

        b1 = -1;
        while (true)
        {
            b1 = b2;
            b2 = _frameReader.BaseStream.ReadByte();

            if (b2 == -1)
                throw new EndOfStreamException();

            buffer.Add((byte)b2);

            if (b1 == 0xFF && b2 == 0xD9)
                break;
        }

        return buffer.ToArray();
    }

    public void StopCurrentVideo()
    {
        _frameReader?.Dispose();
        _frameReader = null;

        if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
        {
            try
            {
                _ffmpegProcess.Kill();
                _ffmpegProcess.WaitForExit(1000);
            }
            catch { }
        }

        _ffmpegProcess?.Dispose();
        _ffmpegProcess = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopCurrentVideo();
    }
}
