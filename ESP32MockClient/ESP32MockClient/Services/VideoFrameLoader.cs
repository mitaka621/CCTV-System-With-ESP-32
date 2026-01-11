namespace ESP32MockClient.Services;

public class VideoFrameLoader : IDisposable
{
    private const string PlaceholderImagePath = "placeholders/missing-footage-placeholder.jpg";

    private readonly VideoSourceManager _videoSource;
    private byte[]? _placeholderFrame;
    private bool _useVideoSource;

    public VideoFrameLoader()
    {
        _videoSource = new VideoSourceManager();
    }

    public bool Initialize()
    {
        // Load placeholder image for fallback
        var placeholderPath = Path.GetFullPath(PlaceholderImagePath);
        if (File.Exists(placeholderPath))
        {
            _placeholderFrame = File.ReadAllBytes(placeholderPath);
        }

        // Try to initialize video source
        if (_videoSource.Initialize() && _videoSource.StartRandomVideo())
        {
            _useVideoSource = true;
            return true;
        }

        Console.WriteLine("[VideoLoader] No videos found, using placeholder");
        return false;
    }

    public byte[]? GetNextFrame()
    {
        if (_useVideoSource)
        {
            var frame = _videoSource.GetNextFrame();
            if (frame != null)
                return frame;
        }

        return _placeholderFrame;
    }

    public void ReturnFrame() { }

    public void Dispose()
    {
        _videoSource.Dispose();
    }
}
