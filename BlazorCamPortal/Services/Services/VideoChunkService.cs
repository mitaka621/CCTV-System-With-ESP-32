using BlazorCamPortal.Contracts.Abstractions.Repositories;
using BlazorCamPortal.Contracts.Abstractions.Services;
using BlazorCamPortal.Contracts.Dtos.VideoChunkDtos;

namespace BlazorCamPortal.Core.Services
{
    public class VideoChunkService : IVideoChunkService
    {
        private readonly IVideoChunkRepository _videoChunkRepository;

        public VideoChunkService(IVideoChunkRepository videoChunkRepository)
        {
            _videoChunkRepository = videoChunkRepository;
        }

        public async Task<Guid> CreateVideoChunkAsync(CreateVideoChunkDto createVideoChunkDto)
        {
            var id = await _videoChunkRepository.CreateVideoChunkAsync(createVideoChunkDto);

            return id;
        }
    }
}
