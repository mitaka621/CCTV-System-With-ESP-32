using CamPortal.Contracts.Dtos.ExportedVideoDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IVideoExportNotifier
    {
        event Action<ExportedVideoDto>? ExportStatusChanged;

        event Action<VideoExportProgressDto>? ExportProgressChanged;

        event Action<VideoExportRemovedDto>? ExportRemoved;

        void NotifyExportStatusChanged(ExportedVideoDto exportedVideo);

        void NotifyExportProgressChanged(VideoExportProgressDto progress);

        void NotifyExportRemoved(VideoExportRemovedDto removed);
    }
}
