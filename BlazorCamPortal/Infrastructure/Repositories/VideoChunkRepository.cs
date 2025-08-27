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
        private readonly CamPortalDBContext _dbContext;
        private readonly IMapper _mapper;

        public VideoChunkRepository(CamPortalDBContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }

        public async Task<Guid> CreateVideoChunkAsync(CreateVideoChunkDto createVideoChunkDto)
        {
            var entity = _mapper.Map<VideoChunk>(createVideoChunkDto);

            _dbContext.VideoChunks.Add(entity);

            await _dbContext.SaveChangesAsync();

            return entity.Id;
        }

        public async Task<Dictionary<Guid, List<VideoChunkShortInfoDto>>> GetVideoChunksForPeriodForCameraAsync(List<Guid> cameraId, DateTime startDate, DateTime endDate)
        {
            var result = await _dbContext.VideoChunks
                .AsNoTracking()
                .Where(x => cameraId.Contains(x.CameraId))
                .Where(x => x.ChunkStartDate < endDate && x.ChunkEndDate > startDate)
                .GroupBy(x => x.CameraId)
                .ToDictionaryAsync(g => g.Key, _mapper.Map<List<VideoChunkShortInfoDto>>);

            return result;
        }

        public async Task<DateTime> GetMaxDateTimeOfAvailableVideoChunksAsync()
        {
            var result = await _dbContext.VideoChunks
                .MaxAsync(x => x.ChunkEndDate);

            return result;
        }

        public async Task<DateTime> GetMinDateTimeOfAvailableVideoChunksAsync()
        {
            var result = await _dbContext.VideoChunks
                .MinAsync(x => x.ChunkStartDate);

            return result;
        }
    }
}
