using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.ExportedVideoDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Core.Services.Video
{
    public class VideoExportService : IVideoExportService
    {
        private readonly IExportedVideoRepository _exportedVideoRepository;
        private readonly IVideoExportJobQueue _videoExportJobQueue;
        private readonly IVideoExportNotifier _videoExportNotifier;
        private readonly IVideoExportCanceller _videoExportCanceller;

        public VideoExportService(
            IExportedVideoRepository exportedVideoRepository,
            IVideoExportJobQueue videoExportJobQueue,
            IVideoExportNotifier videoExportNotifier,
            IVideoExportCanceller videoExportCanceller)
        {
            _exportedVideoRepository = exportedVideoRepository;
            _videoExportJobQueue = videoExportJobQueue;
            _videoExportNotifier = videoExportNotifier;
            _videoExportCanceller = videoExportCanceller;
        }

        public async Task<Guid> QueueExportAsync(QueueVideoExportInputDto queueExportDto)
        {
            var exportId = await _exportedVideoRepository.CreateExportAsync(queueExportDto);

            var createdExport = await _exportedVideoRepository.GetExportByIdAsync(exportId);

            if (createdExport != null)
            {
                _videoExportNotifier.NotifyExportStatusChanged(createdExport);
            }

            _videoExportJobQueue.Enqueue(exportId);

            return exportId;
        }

        public async Task<List<ExportedVideoDto>> GetExportsForUserAsync(Guid userId)
        {
            return await _exportedVideoRepository.GetExportsForUserAsync(userId);
        }

        public async Task<bool> CancelExportAsync(Guid exportId, Guid userId)
        {
            var export = await _exportedVideoRepository.GetExportByIdAsync(exportId);

            if (export == null || export.UserId != userId || export.ExportStatus != ExportVideoStatuses.Started)
            {
                return false;
            }

            _videoExportCanceller.RequestCancel(exportId);

            return true;
        }
    }
}
