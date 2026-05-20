using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Enums;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CamPortal.Core.Services
{
    public class CameraFramesManagerService : ICameraFramesManagerService
    {
        private const int _viewerChannelBufferSize = 4;

        private readonly int _numberOfBufferFramesInChannel;
        private readonly ILogger<ICameraFramesManagerService> _logger;
        private readonly ConcurrentDictionary<Guid, Channel<byte[]>> _processedFramesCameraChannels = new();
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, ChannelWriter<byte[]>>> _viewerWritersByCamera = new();
        private readonly ConcurrentDictionary<Guid, byte[]> _latestFrameByCamera = new();
        private readonly byte[] _defaultFrame;
        private readonly Font _stampFont = SystemFonts.CreateFont("Arial", 28, FontStyle.Bold);

        private readonly Channel<(Guid, byte[])> _rawFramesChannel;
        private int _highWaterMark = 0;
        private DateTime _lastSaturationLogUtc = default;

        public event Func<Guid, byte[], Task>? FrameProcessed;
        public event Action<Guid>? ChannelClosed;
        public event Action<Guid>? ChannelOpened;

        public ChannelReader<(Guid, byte[])> RawFramesChannelReader => _rawFramesChannel.Reader;

        public CameraFramesManagerService(IConfiguration configuration, ILogger<ICameraFramesManagerService> logger)
        {
            _numberOfBufferFramesInChannel = int.Parse(configuration.GetSection("TCPServerConfig")["NumberOfBufferRawFrames"]
                ?? throw new InvalidOperationException("NumberOfBufferRawFrames configuration is missing"));

            _rawFramesChannel = Channel.CreateBounded<(Guid, byte[])>(
                new BoundedChannelOptions(_numberOfBufferFramesInChannel)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                });

            _defaultFrame = VideoChunkUtilities.GetDefaultFrame(configuration);
            _logger = logger;
        }

        public async Task InitializeAsync(IDeviceService cameraService)
        {
            var cameras = await cameraService.GetAllCamerasAsync(DevicePairStatus.Paired);

            cameras.ForEach(camera =>
            {
                var channel = GetOrCreateProcessedFramesCameraChannel(camera.Id);
                channel.Writer.TryWrite(_defaultFrame);
            });
        }

        public void AddFrame(Guid cameraId, byte[] frame)
        {
            if (cameraId == Guid.Empty)
                throw new ArgumentException("Camera ID cannot be empty", nameof(cameraId));
            if (frame == null || frame.Length == 0)
                throw new ArgumentException("Frame data cannot be null or empty", nameof(frame));

            _rawFramesChannel.Writer.TryWrite((cameraId, frame));

            int depth = _rawFramesChannel.Reader.Count;

            int oldMax;
            do
            {
                oldMax = _highWaterMark;
                if (depth <= oldMax) break;
            }
            while (Interlocked.CompareExchange(ref _highWaterMark, depth, oldMax) != oldMax);

            if (depth >= _numberOfBufferFramesInChannel * 0.9)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastSaturationLogUtc).TotalSeconds >= 5)
                {
                    _lastSaturationLogUtc = now;
                    _logger.LogCritical(
                        "Raw frame channel near capacity: {Depth}/{Capacity}",
                        depth,
                        _numberOfBufferFramesInChannel);
                }
            }
        }

        public void CloseProcessedFramesCameraChannel(Guid cameraId)
        {
            if (_processedFramesCameraChannels.TryRemove(cameraId, out var channel))
            {
                channel.Writer.TryComplete();
                ChannelClosed?.Invoke(cameraId);
            }
        }

        public Channel<byte[]> GetOrCreateProcessedFramesCameraChannel(Guid cameraId)
        {
            if (_processedFramesCameraChannels.TryGetValue(cameraId, out var existing))
                return existing;

            var newChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(_numberOfBufferFramesInChannel)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
            if (_processedFramesCameraChannels.TryAdd(cameraId, newChannel))
            {
                ChannelOpened?.Invoke(cameraId);
                return newChannel;
            }
            return _processedFramesCameraChannels[cameraId];
        }

        public byte[] StampFrame(byte[] frame)
        {
            using var image = Image.Load(frame);

            string text = $"<{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}>";

            image.Mutate(ctx => ctx.DrawText(text, _stampFont, Color.White, new PointF(image.Width - 320, image.Height - 50)));

            using var ms = new MemoryStream();

            image.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
            {
                Quality = 95,
            });
            return ms.ToArray();
        }

        public void PublishProcessedFrame(Guid cameraId, byte[] frame)
        {
            var channel = GetOrCreateProcessedFramesCameraChannel(cameraId);
            channel.Writer.TryWrite(frame);

            _latestFrameByCamera[cameraId] = frame;

            if (_viewerWritersByCamera.TryGetValue(cameraId, out var viewers))
            {
                foreach (var writer in viewers.Values)
                {
                    writer.TryWrite(frame);
                }
            }

            var handlers = FrameProcessed;
            if (handlers != null)
            {
                foreach (Func<Guid, byte[], Task> handler in handlers.GetInvocationList())
                {
                    var localHandler = handler;
                    _ = Task.Run(async () =>
                    {
                        try { await localHandler(cameraId, frame); }
                        catch (Exception ex) { _logger.LogError(ex, "Frame handler failed for camera {CameraId}", cameraId); }
                    });
                }
            }
        }

        public ChannelReader<byte[]> SubscribeViewer(Guid cameraId, Guid viewerId)
        {
            var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(_viewerChannelBufferSize)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            });

            var viewers = _viewerWritersByCamera.GetOrAdd(cameraId, _ => new ConcurrentDictionary<Guid, ChannelWriter<byte[]>>());

            if (viewers.TryGetValue(viewerId, out var previousWriter))
            {
                previousWriter.TryComplete();
            }

            viewers[viewerId] = channel.Writer;

            if (_latestFrameByCamera.TryGetValue(cameraId, out var lastFrame))
            {
                channel.Writer.TryWrite(lastFrame);
            }
            else
            {
                channel.Writer.TryWrite(_defaultFrame);
            }

            return channel.Reader;
        }

        public void UnsubscribeViewer(Guid cameraId, Guid viewerId)
        {
            if (_viewerWritersByCamera.TryGetValue(cameraId, out var viewers) &&
                viewers.TryRemove(viewerId, out var writer))
            {
                writer.TryComplete();
            }
        }
    }
}
