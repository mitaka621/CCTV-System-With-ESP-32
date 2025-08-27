using AutoMapper;
using BlazorCamPortal.Contracts.Dtos.CameraDtos;
using BlazorCamPortal.Contracts.Dtos.VideoChunkDtos;
using BlazorCamPortal.Contracts.Models;
using BlazorCamPortal.Infrastructure.Data.Entities;

namespace BlazorCamPortal.MapperConfiguration
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<Camera, CameraDto>()
                .ReverseMap();

            CreateMap<Camera, CreateCameraDto>()
                .ReverseMap();

            CreateMap<CameraDisplayModel, CameraDto>()
                .ReverseMap();

            CreateMap<CameraResponseModel, CreateCameraDto>()
                .ReverseMap();

            CreateMap<VideoChunk, CreateVideoChunkDto>()
                .ReverseMap();

            CreateMap<NameAndIdWithStatusDto, Camera>()
                .ReverseMap();

            CreateMap<NameAndIdWithStatusDto, NameAndIdWithStatusModel>()
                .ReverseMap();

            CreateMap<VideoChunkShortInfoDto, VideoChunk>()
                .ReverseMap();
        }
    }
}
