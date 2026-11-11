using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Dtos.RemoteStorageDtos;
using CamPortal.Contracts.Dtos.VideoChunkDtos;
using CamPortal.Infrastructure.Data;
using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class VideoChunkReplicationRepository : IVideoChunkReplicationRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public VideoChunkReplicationRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task<bool> IsVideoChunkReplicatedAsync(Guid targetId, Guid videoChunkId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.VideoChunkReplications
                .AnyAsync(x => x.RemoteStorageTargetId == targetId && x.VideoChunkId == videoChunkId);
        }

        public async Task MarkVideoChunkReplicatedAsync(Guid targetId, Guid videoChunkId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var alreadyReplicated = await db.VideoChunkReplications
                .AnyAsync(x => x.RemoteStorageTargetId == targetId && x.VideoChunkId == videoChunkId);

            if (alreadyReplicated)
            {
                return;
            }

            db.VideoChunkReplications.Add(new VideoChunkReplication
            {
                Id = Guid.NewGuid(),
                RemoteStorageTargetId = targetId,
                VideoChunkId = videoChunkId,
                ReplicatedDateUTC = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        public async Task<List<ExpiredVideoChunkDto>> GetAllExpiredReplicatedChunksForTargetAsync(Guid targetId, DateTime cutoffUTC)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.VideoChunkReplications
                .AsNoTracking()
                .Where(x => x.RemoteStorageTargetId == targetId)
                .Where(x => x.VideoChunk.ChunkEndTime < cutoffUTC)
                .Select(x => new ExpiredVideoChunkDto()
                {
                    DeviceId = x.VideoChunk.DeviceId,
                    FileName = x.VideoChunk.FileName,
                    Id = x.VideoChunk.Id
                })
                .ToListAsync();
        }

        public async Task<int> MarkReplicatedChunksAsDeletedAsync(Guid targetId, List<Guid> videChunkIds)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.VideoChunkReplications
                .Where(x => x.RemoteStorageTargetId == targetId && videChunkIds.Contains(x.VideoChunkId))
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsDeleted, true));
        }

        public async Task<Dictionary<Guid, List<VideoChunkShortInfoDto>>> GetVideoChunksForPeriodForCameraFromRemoteStorageAsync(Guid targetId, List<Guid> cameraId, DateTime startDate, DateTime endDate)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var result = await db.VideoChunkReplications
                .AsNoTracking()
                .Where(x => !x.IsDeleted && cameraId.Contains(x.VideoChunk.DeviceId) && x.RemoteStorageTargetId == targetId && x.RemoteStorageTarget.IsSyncEnabled)
                .Where(x => x.VideoChunk.ChunkStartTime < endDate && x.VideoChunk.ChunkEndTime > startDate)
                .GroupBy(x => x.VideoChunk.DeviceId)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    Chunks = g.OrderBy(c => c.VideoChunk.ChunkStartTime)
                        .Select(c => _mapper.Map<VideoChunkShortInfoDto>(c.VideoChunk))
                        .ToList()
                })
                .ToDictionaryAsync(g => g.DeviceId, g => g.Chunks);

            return result;
        }
    }
}
