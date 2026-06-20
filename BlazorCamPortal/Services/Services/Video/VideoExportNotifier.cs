using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.ExportedVideoDtos;
using Microsoft.Extensions.Logging;

namespace CamPortal.Core.Services.Video
{
    public class VideoExportNotifier : IVideoExportNotifier
    {
        private readonly ILogger<VideoExportNotifier> _logger;

        public event Action<ExportedVideoDto>? ExportStatusChanged;

        public event Action<VideoExportProgressDto>? ExportProgressChanged;

        public event Action<VideoExportRemovedDto>? ExportRemoved;

        public VideoExportNotifier(ILogger<VideoExportNotifier> logger)
        {
            _logger = logger;
        }

        public void NotifyExportStatusChanged(ExportedVideoDto exportedVideo)
        {
            var handlers = ExportStatusChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<Action<ExportedVideoDto>>())
            {
                try
                {
                    handler(exportedVideo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ExportStatusChanged handler threw for export {ExportId}", exportedVideo.Id);
                }
            }
        }

        public void NotifyExportProgressChanged(VideoExportProgressDto progress)
        {
            var handlers = ExportProgressChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<Action<VideoExportProgressDto>>())
            {
                try
                {
                    handler(progress);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ExportProgressChanged handler threw for export {ExportId}", progress.ExportId);
                }
            }
        }

        public void NotifyExportRemoved(VideoExportRemovedDto removed)
        {
            var handlers = ExportRemoved;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<Action<VideoExportRemovedDto>>())
            {
                try
                {
                    handler(removed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ExportRemoved handler threw for export {ExportId}", removed.ExportId);
                }
            }
        }
    }
}
