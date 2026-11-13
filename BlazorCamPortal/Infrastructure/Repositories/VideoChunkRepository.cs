using MapsterMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Dtos.RemoteStorageDtos;
using CamPortal.Contracts.Dtos.VideoChunkDtos;
using CamPortal.Infrastructure.Data;
using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class VideoChunkRepository : IVideoChunkRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public VideoChunkRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task<Guid> CreateVideoChunkAsync(CreateVideoChunkDto createVideoChunkDto)
        {
            var entity = _mapper.Map<VideoChunk>(createVideoChunkDto);

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            db.VideoChunks.Add(entity);

            await db.SaveChangesAsync();

            return entity.Id;
        }

        public async Task<Dictionary<Guid, List<VideoChunkShortInfoDto>>> GetVideoChunksForPeriodForCameraFromLocalStorageAsync(List<Guid> cameraId, DateTime startDate, DateTime endDate)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var result = await db.VideoChunks
                .AsNoTracking()
                .Where(x => cameraId.Contains(x.DeviceId) && !x.IsDeletedFromLocalStorage)
                .Where(x => x.ChunkStartTime < endDate && x.ChunkEndTime > startDate)
                .GroupBy(x => x.DeviceId)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    Chunks = g.OrderBy(c => c.ChunkStartTime)
                        .Select(c => _mapper.Map<VideoChunkShortInfoDto>(c))
                        .ToList()
                })
                .ToDictionaryAsync(g => g.DeviceId, g => g.Chunks);

            return result;
        }

        public async Task<double> GetTotalVideoChinksSizeInMBAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.VideoChunks
                .Where(x => !x.IsDeletedFromLocalStorage)
                .SumAsync(x => x.SizeInMB);

            return result;
        }

        public async Task<List<ExpiredVideoChunkDto>> GetExpiredVideoChunksAsync(DateTime expirationCutoffUtc)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.VideoChunks
                .AsNoTracking()
                .Where(x => x.ChunkEndTime < expirationCutoffUtc)
                .Select(x => new ExpiredVideoChunkDto
                {
                    Id = x.Id,
                    DeviceId = x.DeviceId,
                    FileName = x.FileName
                })
                .ToListAsync();
        }

        public async Task MarkVideoChunksAsDeletedInLocalStorageAsync(List<Guid> chunkIds)
        {
            if (chunkIds.Count == 0)
            {
                return;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            await db.VideoChunks
                .Where(x => chunkIds.Contains(x.Id))
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsDeletedFromLocalStorage, true));
        }

        public async Task<List<VideoChunkReplicationItemDto>> GetUnreplicatedVideoChunksAsync(Guid targetId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.VideoChunks
                .AsNoTracking()
                .Where(x => !db.VideoChunkReplications.Any(r => r.RemoteStorageTargetId == targetId && r.VideoChunkId == x.Id))
                .OrderBy(x => x.ChunkStartTime)
                .Select(x => new VideoChunkReplicationItemDto
                {
                    VideoChunkId = x.Id,
                    DeviceId = x.DeviceId,
                    FileName = x.FileName,
                    ChunkEndTime = x.ChunkEndTime
                })
                .ToListAsync();
        }

        public async Task<int> DeleteChunksWhichAreNotAvailbileInAnyStorageLocationAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.VideoChunks
                 .Where(x => x.IsDeletedFromLocalStorage && x.VideoChunkReplications.All(x => x.IsDeleted))
                 .ExecuteDeleteAsync();
        }
    }
}
