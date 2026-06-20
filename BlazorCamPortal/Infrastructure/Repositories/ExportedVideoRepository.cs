using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Dtos.ExportedVideoDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Infrastructure.Data;
using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class ExportedVideoRepository : IExportedVideoRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public ExportedVideoRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task<Guid> CreateExportAsync(QueueVideoExportInputDto createExportDto)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var entity = new ExportedVideo
            {
                Id = Guid.NewGuid(),
                UserId = createExportDto.UserId,
                CameraId = createExportDto.CameraId,
                VideoStartDate = createExportDto.VideoStartDate,
                VideoEndDate = createExportDto.VideoEndDate,
                ExportStartedDate = DateTime.UtcNow,
                ExportStatus = ExportVideoStatuses.Started
            };

            db.ExportedVideos.Add(entity);

            await db.SaveChangesAsync();

            return entity.Id;
        }

        public async Task<List<ExportedVideoDto>> GetExportsForUserAsync(Guid userId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.ExportedVideos
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.ExportStartedDate)
                .Select(x => new ExportedVideoDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    CameraId = x.CameraId,
                    CameraName = x.Camera.Name,
                    ExportedURLForDownload = x.ExportedURLForDownload,
                    ExportStartedDate = x.ExportStartedDate,
                    ExportFinishedDate = x.ExportFinishedDate,
                    ExportStatus = x.ExportStatus,
                    VideoStartDate = x.VideoStartDate,
                    VideoEndDate = x.VideoEndDate,
                    SizeInMB = x.SizeInMB
                })
                .ToListAsync();
        }

        public async Task<List<Guid>> GetPendingExportIdsAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.ExportedVideos
                .AsNoTracking()
                .Where(x => x.ExportStatus == ExportVideoStatuses.Started)
                .OrderBy(x => x.ExportStartedDate)
                .Select(x => x.Id)
                .ToListAsync();
        }

        public async Task<ExportedVideoDto?> GetExportByIdAsync(Guid exportId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.ExportedVideos
                .AsNoTracking()
                .Where(x => x.Id == exportId)
                .Select(x => new ExportedVideoDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    CameraId = x.CameraId,
                    CameraName = x.Camera.Name,
                    ExportedURLForDownload = x.ExportedURLForDownload,
                    ExportStartedDate = x.ExportStartedDate,
                    ExportFinishedDate = x.ExportFinishedDate,
                    ExportStatus = x.ExportStatus,
                    VideoStartDate = x.VideoStartDate,
                    VideoEndDate = x.VideoEndDate,
                    SizeInMB = x.SizeInMB
                })
                .FirstOrDefaultAsync();
        }

        public async Task FinishExportAsync(FinishVideoExportDto finishExportDto)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            await db.ExportedVideos
                .Where(x => x.Id == finishExportDto.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ExportStatus, finishExportDto.ExportStatus)
                    .SetProperty(x => x.ExportedURLForDownload, finishExportDto.ExportedURLForDownload)
                    .SetProperty(x => x.FilePath, finishExportDto.FilePath)
                    .SetProperty(x => x.ExportFinishedDate, finishExportDto.ExportFinishedDate)
                    .SetProperty(x => x.SizeInMB, finishExportDto.SizeInMB));
        }

        public async Task DeleteExportAsync(Guid exportId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            await db.ExportedVideos
                .Where(x => x.Id == exportId)
                .ExecuteDeleteAsync();
        }

        public async Task<List<ExpiredExportedVideoDto>> GetExpiredExportsAsync(DateTime expirationCutoffUtc)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.ExportedVideos
                .AsNoTracking()
                .Where(x => x.ExportStatus == ExportVideoStatuses.Finished && x.ExportFinishedDate < expirationCutoffUtc)
                .Select(x => new ExpiredExportedVideoDto
                {
                    Id = x.Id,
                    FilePath = x.FilePath
                })
                .ToListAsync();
        }

        public async Task MarkExportsAsExpiredAsync(List<Guid> exportIds)
        {
            if (exportIds.Count == 0)
            {
                return;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            await db.ExportedVideos
                .Where(x => exportIds.Contains(x.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ExportStatus, ExportVideoStatuses.Expired)
                    .SetProperty(x => x.ExportedURLForDownload, (string?)null)
                    .SetProperty(x => x.FilePath, (string?)null));
        }
    }
}
