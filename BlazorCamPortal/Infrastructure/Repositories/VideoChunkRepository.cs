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

        public async Task<List<VideoChunkShortInfoDto>> GetVideoChunksForPeriodForCameraAsync(Guid cameraId, DateTime startDate, DateTime endDate)
        {
            var result = await _dbContext.VideoChunks
                .AsNoTracking()
                .Where(x => x.CameraId == cameraId)
                .Where(x => (x.ChunkStartDate >= startDate && x.ChunkStartDate < endDate) || (x.ChunkEndDate > startDate && x.ChunkEndDate <= endDate))
                .Select(x => _mapper.Map<VideoChunkShortInfoDto>(x))
                .ToListAsync();

            return result;
        }
    }
}
