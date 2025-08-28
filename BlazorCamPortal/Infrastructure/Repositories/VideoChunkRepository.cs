using AutoMapper;
using BlazorCamPortal.Contracts.Abstractions.Repositories;
using BlazorCamPortal.Contracts.Dtos.VideoChunkDtos;
using BlazorCamPortal.Infrastructure.Data;
using BlazorCamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorCamPortal.Infrastructure.Repositories
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

        public async Task<Dictionary<Guid, List<VideoChunkShortInfoDto>>> GetVideoChunksForPeriodForCameraAsync(List<Guid> cameraId, DateTime startDate, DateTime endDate)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var result = await db.VideoChunks
                .AsNoTracking()
                .Where(x => cameraId.Contains(x.CameraId))
                .Where(x => x.ChunkStartTime < endDate && x.ChunkEndTime > startDate)
                .GroupBy(x => x.CameraId)
                .Select(g => new
                {
                    CameraId = g.Key,
                    Chunks = g.OrderBy(c => c.ChunkStartTime)
                        .Select(c => _mapper.Map<VideoChunkShortInfoDto>(c))
                        .ToList()
                })
                .ToDictionaryAsync(g => g.CameraId, g => g.Chunks);

            return result;
        }

        public async Task<DateTime> GetMaxDateTimeOfAvailableVideoChunksAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.VideoChunks
                .MaxAsync(x => x.ChunkEndTime);

            return result;
        }

        public async Task<DateTime> GetMinDateTimeOfAvailableVideoChunksAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.VideoChunks
                .MinAsync(x => x.ChunkStartTime);

            return result;
        }

        public async Task<double> GetTotalVideoChinksSizeInMBAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var result = await db.VideoChunks
                .SumAsync(x => x.SizeInMB);

            return result;
        }
    }
}
