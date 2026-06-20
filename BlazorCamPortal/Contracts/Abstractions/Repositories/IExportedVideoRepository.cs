using CamPortal.Contracts.Dtos.ExportedVideoDtos;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface IExportedVideoRepository
    {
        Task<Guid> CreateExportAsync(QueueVideoExportInputDto createExportDto);

        Task<List<ExportedVideoDto>> GetExportsForUserAsync(Guid userId);

        Task<List<Guid>> GetPendingExportIdsAsync();

        Task<ExportedVideoDto?> GetExportByIdAsync(Guid exportId);

        Task FinishExportAsync(FinishVideoExportDto finishExportDto);

        Task DeleteExportAsync(Guid exportId);

        Task<List<ExpiredExportedVideoDto>> GetExpiredExportsAsync(DateTime expirationCutoffUtc);

        Task MarkExportsAsExpiredAsync(List<Guid> exportIds);
    }
}
