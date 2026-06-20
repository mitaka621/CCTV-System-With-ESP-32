using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.CameraFrameDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
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

namespace CamPortal.Core.Services.Devices
{
    public class CameraFramesManagerService : ICameraFramesManagerService
    {
        private const int _viewerChannelBufferSize = 4;

        private readonly int _numberOfBufferFramesInChannel;
        private readonly ILogger<ICameraFramesManagerService> _logger;
        private readonly ConcurrentDictionary<Guid, Channel<byte[]>> _processedFramesCameraChannels = new();
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, CameraFramesForViewerDto>> _viewerWritersByCamera = new();
        private readonly ConcurrentDictionary<Guid, byte[]> _latestFrameByCamera = new();
        private readonly byte[] _defaultFrame;
        private readonly Font _stampFont = SystemFonts.CreateFont("Arial", 28, FontStyle.Bold);

        private readonly Channel<(DeviceStreamingHandshakeDto, byte[])> _rawFramesChannel;
        private int _highWaterMark = 0;
        private DateTime _lastSaturationLogUtc = default;

        public event Func<Guid, byte[], Task>? FrameProcessed;
        public event Action<Guid>? ChannelClosed;
        public event Action<Guid>? ChannelOpened;

        public ChannelReader<(DeviceStreamingHandshakeDto, byte[])> RawFramesChannelReader => _rawFramesChannel.Reader;

        public CameraFramesManagerService(IConfiguration configuration, ILogger<ICameraFramesManagerService> logger)
        {
            _numberOfBufferFramesInChannel = int.Parse(configuration.GetSection("TCPServerConfig")["NumberOfBufferRawFrames"]
                ?? throw new InvalidOperationException("NumberOfBufferRawFrames configuration is missing"));

            _rawFramesChannel = Channel.CreateBounded<(DeviceStreamingHandshakeDto, byte[])>(
                new BoundedChannelOptions(_numberOfBufferFramesInChannel)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                });

            _defaultFrame = VideoChunkUtilities.GetDefaultFrame(configuration);
            _logger = logger;
        }

        public void AddFrame(DeviceStreamingHandshakeDto device, byte[] frame)
        {
            if (device.Id == Guid.Empty)
                throw new ArgumentException("Camera ID cannot be empty", nameof(device.Id));
            if (frame == null || frame.Length == 0)
                throw new ArgumentException("Frame data cannot be null or empty", nameof(frame));

            _rawFramesChannel.Writer.TryWrite((device, frame));

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

        public CameraFrameDto StampFrame(byte[] frame, DeviceStreamingHandshakeDto camera)
        {
            using var image = Image.Load(frame);

            string text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            var cameraName = camera.DeviceName ?? $"<No Name Set, Device id {camera.Id}>";

            var cameraConfig = camera.CameraStreamingConfiguration;

            image.Mutate(ctx =>
            {
                if (cameraConfig.FrameRotation != 0)
                {
                    ctx.Rotate(cameraConfig.FrameRotation);
                }

                var currentSize = ctx.GetCurrentSize();

                if (cameraConfig.ZoomFactor > 1 || cameraConfig.CameraAspectRatio != CameraAspectRatios.Original)
                {
                    (int frameWidth, int frameHeight) = CameraAspectRatioResolver.CalculateActualResolution(currentSize.Width, currentSize.Height, cameraConfig.CameraAspectRatio);

                    var cropWidth = Math.Clamp((int)(frameWidth / cameraConfig.ZoomFactor), 1, currentSize.Width);
                    var cropHeight = Math.Clamp((int)(frameHeight / cameraConfig.ZoomFactor), 1, currentSize.Height);
                    var startX = Math.Clamp(cameraConfig.ZoomStartX, 0, currentSize.Width - cropWidth);
                    var startY = Math.Clamp(cameraConfig.ZoomStartY, 0, currentSize.Height - cropHeight);
                    ctx.Crop(new Rectangle(startX, startY, cropWidth, cropHeight));
                    currentSize = ctx.GetCurrentSize();
                }

                ctx.Brightness(cameraConfig.Brightness);
                ctx.Contrast(cameraConfig.Contrast);

                if (cameraConfig.FlipMode != FlipMode.None)
                {
                    ctx.Flip(cameraConfig.FlipMode);
                }

                if (cameraConfig.SharpenFactor > 0)
                {
                    ctx.GaussianSharpen(cameraConfig.SharpenFactor);
                }

                ctx.DrawText(text, _stampFont, Color.White, new PointF(currentSize.Width - 320, currentSize.Height - 50));
                ctx.DrawText(cameraName, _stampFont, Color.White, new PointF(10, currentSize.Height - 50));
            });

            using var originalResolution = new MemoryStream();
            image.SaveAsJpeg(originalResolution, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
            {
                Quality = 95,
            });

            using var reducedResolution = new MemoryStream();
            image.Mutate(ctx => ctx.Resize(new ResizeOptions()
            {
                Size = new Size(640, 0),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Triangle
            }));
            image.SaveAsJpeg(reducedResolution, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
            {
                Quality = 70,
            });

            return new CameraFrameDto
            {
                ProccessedFrameOriginalResolution = originalResolution.ToArray(),
                ProccessedFrameReducedResolution = reducedResolution.ToArray()
            };
        }

        public void PublishProcessedFrame(Guid cameraId, CameraFrameDto dto)
        {
            //client live view update (dashboard camera live grid) in original (for full screen) and reduced resolutions (for camera grid previews)
            var channel = GetOrCreateProcessedFramesCameraChannel(cameraId);
            channel.Writer.TryWrite(dto.ProccessedFrameOriginalResolution);

            _latestFrameByCamera[cameraId] = dto.ProccessedFrameReducedResolution;

            if (_viewerWritersByCamera.TryGetValue(cameraId, out var viewers))
            {
                foreach (var writers in viewers.Values)
                {
                    if (writers.OriginalFrameResolutionChannel != null)
                    {
                        writers.OriginalFrameResolutionChannel.TryWrite(dto.ProccessedFrameOriginalResolution);
                    }

                    if (writers.ReducedFrameResolutionChannel != null)
                    {
                        writers.ReducedFrameResolutionChannel.TryWrite(dto.ProccessedFrameReducedResolution);
                    }
                }
            }

            //triggering background server side FrameProcessed events for camera footage saving to local server disk (original resolution)
            var handlers = FrameProcessed;
            if (handlers != null)
            {
                foreach (Func<Guid, byte[], Task> handler in handlers.GetInvocationList())
                {
                    var localHandler = handler;
                    _ = Task.Run(async () =>
                    {
                        try { await localHandler(cameraId, dto.ProccessedFrameOriginalResolution); }
                        catch (Exception ex) { _logger.LogError(ex, "Frame handler failed for camera {CameraId}", cameraId); }
                    });
                }
            }
        }

        public void PublishPlaceholderToViewers(Guid cameraId)
        {
            _latestFrameByCamera[cameraId] = _defaultFrame;

            if (_viewerWritersByCamera.TryGetValue(cameraId, out var viewers))
            {
                foreach (var writers in viewers.Values)
                {
                    writers.OriginalFrameResolutionChannel?.TryWrite(_defaultFrame);
                    writers.ReducedFrameResolutionChannel?.TryWrite(_defaultFrame);
                }
            }
        }

        public ChannelReader<byte[]> SubscribeViewer(Guid cameraId, Guid viewerId, bool isOriginalResolution)
        {
            var viewers = _viewerWritersByCamera.GetOrAdd(cameraId, _ => new ConcurrentDictionary<Guid, CameraFramesForViewerDto>());

            if (viewers.TryGetValue(viewerId, out var previousWriters))
            {
                if (isOriginalResolution)
                {
                    previousWriters.OriginalFrameResolutionChannel?.TryComplete();
                }
                else
                {
                    previousWriters.ReducedFrameResolutionChannel?.TryComplete();
                }
            }
            else
            {
                viewers.TryAdd(viewerId, new CameraFramesForViewerDto());
            }

            byte[] firstFrame;

            if (_latestFrameByCamera.TryGetValue(cameraId, out var lastFrame))
            {
                firstFrame = lastFrame;
            }
            else
            {
                firstFrame = _defaultFrame;
            }

            if (isOriginalResolution)
            {
                var originalFrameResolutionChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(_viewerChannelBufferSize)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = true,
                });

                viewers[viewerId].OriginalFrameResolutionChannel = originalFrameResolutionChannel.Writer;
                viewers[viewerId].OriginalFrameResolutionChannel!.TryWrite(firstFrame);

                return originalFrameResolutionChannel.Reader;
            }

            var reducedFrameResolutionChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(_viewerChannelBufferSize)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            });

            viewers[viewerId].ReducedFrameResolutionChannel = reducedFrameResolutionChannel.Writer;
            viewers[viewerId].ReducedFrameResolutionChannel!.TryWrite(firstFrame);

            return reducedFrameResolutionChannel.Reader;
        }

        public void UnsubscribeViewer(Guid cameraId, Guid viewerId, bool isOriginalResolution)
        {
            if (_viewerWritersByCamera.TryGetValue(cameraId, out var viewers) && viewers.TryGetValue(viewerId, out var writers))
            {
                if (isOriginalResolution)
                {
                    writers.OriginalFrameResolutionChannel?.TryComplete();

                    return;
                }

                writers.ReducedFrameResolutionChannel?.TryComplete();
            }
        }
    }
}
