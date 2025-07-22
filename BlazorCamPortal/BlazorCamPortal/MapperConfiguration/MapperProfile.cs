using AutoMapper;
using BlazorCamPortal.Contracts.Dtos;
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
        }
    }
}
