using BlazorCamPortal.Contracts.Dtos.VideoChunkDtos;

namespace BlazorCamPortal.Contracts.Abstractions.Services
{
    public interface IVideoChunkService
    {
        public Task<Guid> CreateVideoChunkAsync(CreateVideoChunkDto createVideoChunkDto);
    }
}
