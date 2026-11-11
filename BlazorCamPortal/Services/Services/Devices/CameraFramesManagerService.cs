using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.CameraFrameDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

        private readonly Channel<(DeviceStreamingHandshakeWithCameraConfigDto, byte[])> _rawFramesChannel;
        private int _highWaterMark = 0;
        private DateTime _lastSaturationLogUtc = default;

        public event Func<Guid, byte[], Task>? FrameProcessed;
        public event Action<Guid>? ChannelClosed;
        public event Action<Guid>? ChannelOpened;

        public ChannelReader<(DeviceStreamingHandshakeWithCameraConfigDto, byte[])> RawFramesChannelReader => _rawFramesChannel.Reader;

        public CameraFramesManagerService(IConfiguration configuration, ILogger<ICameraFramesManagerService> logger)
        {
            _numberOfBufferFramesInChannel = int.Parse(configuration.GetSection("TCPServerConfig")["NumberOfBufferRawFrames"]
                ?? throw new InvalidOperationException("NumberOfBufferRawFrames configuration is missing"));

            _rawFramesChannel = Channel.CreateBounded<(DeviceStreamingHandshakeWithCameraConfigDto, byte[])>(
                new BoundedChannelOptions(_numberOfBufferFramesInChannel)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                });

            _defaultFrame = VideoChunkUtilities.GetDefaultFrame(configuration);
            _logger = logger;
        }

        public void AddFrame(DeviceStreamingHandshakeWithCameraConfigDto device, byte[] frame)
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

        public void PublishOriginalResolutionFrame(Guid cameraId, byte[] frame)
        {
            //recording (processed channel) and full screen live view viewers receive the original resolution
            var channel = GetOrCreateProcessedFramesCameraChannel(cameraId);
            channel.Writer.TryWrite(frame);

            if (_viewerWritersByCamera.TryGetValue(cameraId, out var viewers))
            {
                foreach (var writers in viewers.Values)
                {
                    writers.OriginalFrameResolutionChannel?.TryWrite(frame);
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
                        try { await localHandler(cameraId, frame); }
                        catch (Exception ex) { _logger.LogError(ex, "Frame handler failed for camera {CameraId}", cameraId); }
                    });
                }
            }
        }

        public void PublishReducedResolutionFrame(Guid cameraId, byte[] frame)
        {
            _latestFrameByCamera[cameraId] = frame;

            if (_viewerWritersByCamera.TryGetValue(cameraId, out var viewers))
            {
                foreach (var writers in viewers.Values)
                {
                    writers.ReducedFrameResolutionChannel?.TryWrite(frame);
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
