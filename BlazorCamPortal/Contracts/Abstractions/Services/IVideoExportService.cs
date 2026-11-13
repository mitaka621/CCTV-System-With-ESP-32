using Homewatch.Contracts.Dtos.ExportedVideoDtos;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IVideoExportService
    {
        Task<Guid> QueueExportAsync(QueueVideoExportInputDto queueExportDto);

        Task<List<ExportedVideoDto>> GetExportsForUserAsync(Guid userId);

        Task<bool> CancelExportAsync(Guid exportId, Guid userId);
    }
}
