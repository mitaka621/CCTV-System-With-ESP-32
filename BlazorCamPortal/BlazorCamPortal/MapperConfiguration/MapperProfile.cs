using AutoMapper;
using CamPortal.Contracts.Dtos.CameraDtos;
using CamPortal.Contracts.Dtos.VideoChunkDtos;
using CamPortal.Contracts.Models;
using CamPortal.Infrastructure.Data.Entities;

namespace CamPortal.MapperConfiguration
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
