using System.Collections.Concurrent;
using System.Threading.Channels;
using BlazorCamPortal.Contracts.Abstractions.Services;
using BlazorCamPortal.Contracts.Enums;
using BlazorCamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace BlazorCamPortal.Core.Services
{
    public class CameraFramesManagerService : ICameraFramesManagerService
    {
        private readonly ConcurrentDictionary<Guid, Channel<byte[]>> _cameraChannels = new();
        private readonly byte[] _defaultFrame;

        private const int _numberOfBufferFramesInChannel = 100;

        public event Func<Guid, byte[], Task>? FrameAdded;
        public event Action<Guid>? ChannelClosed;
        public event Action<Guid>? ChannelOpened;

        public CameraFramesManagerService(IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _defaultFrame = FrameUtilities.GetDefaultFrame(configuration);
        }

        public ConcurrentDictionary<Guid, Channel<byte[]>> GetCameraChannels { get => _cameraChannels; }

        public async Task InitializeAsync(ICameraService _cameraService)
        {
            var cameras = await _cameraService.GetAllCamerasAsync(PairStatus.Paired);

            cameras.ForEach(camera =>
                {
                    var channel = GetOrCreateChannel(camera.Id);
                    channel.Writer.TryWrite(_defaultFrame);
                });
        }

        public void AddFrame(Guid cameraId, byte[] frame)
        {
            if (cameraId == Guid.Empty)
                throw new ArgumentException("Camera ID cannot be empty", nameof(cameraId));
            if (frame == null || frame.Length == 0)
                throw new ArgumentException("Frame data cannot be null or empty", nameof(frame));

            var stampedFrame = StampFrame(frame);

            var channel = GetOrCreateChannel(cameraId);
            channel.Writer.TryWrite(stampedFrame);

            var handlers = FrameAdded;
            if (handlers != null)
            {
                foreach (Func<Guid, byte[], Task> handler in handlers.GetInvocationList())
                {
                    _ = handler.Invoke(cameraId, stampedFrame);
                }
            }
        }

        public void CloseChannel(Guid cameraId)
        {
            try
            {
                ChannelClosed?.Invoke(cameraId);
            }
            finally
            {
                _cameraChannels.Remove(cameraId, out _);
            }
        }

        public Channel<byte[]> GetOrCreateChannel(Guid cameraId)
        {
            if (!_cameraChannels.ContainsKey(cameraId))
            {
                _cameraChannels.TryAdd(
                    cameraId,
                    Channel.CreateBounded<byte[]>(new BoundedChannelOptions(_numberOfBufferFramesInChannel)
                    {
                        FullMode = BoundedChannelFullMode.DropOldest
                    }));

                ChannelOpened?.Invoke(cameraId);
            }

            return _cameraChannels[cameraId];
        }

        private byte[] StampFrame(byte[] frame)
        {
            using var image = Image.Load(frame);

            string text = $"<{DateTime.Now:yyyy-MM-dd HH:mm:ss}>";

            var font = SystemFonts.CreateFont("Arial", 28, FontStyle.Bold);

            image.Mutate(ctx => ctx.DrawText(text, font, Color.White, new PointF(image.Width - 320, image.Height - 50)));

            using var ms = new MemoryStream();

            image.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
            {
                Quality = 95
            });
            return ms.ToArray();
        }
    }
}
