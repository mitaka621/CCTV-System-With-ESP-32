using AutoMapper;
using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Dtos.CameraDtos;
using CamPortal.Contracts.Dtos.DeviceTypeDtos;
using CamPortal.Contracts.Dtos.PreprovisionAttemptDtos;
using CamPortal.Contracts.Dtos.VideoChunkDtos;
using CamPortal.Contracts.Models;
using CamPortal.Infrastructure.Data.Entities;

namespace CamPortal.MapperConfiguration
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<Device, DeviceDto>()
                .ReverseMap();

            CreateMap<Device, CreateDeviceDto>()
                .ReverseMap();

            CreateMap<CameraDisplayModel, DeviceDto>()
                .ReverseMap();

            CreateMap<CameraResponseModel, CreateDeviceDto>()
                .ReverseMap();

            CreateMap<VideoChunk, CreateVideoChunkDto>()
                .ReverseMap();

            CreateMap<NameAndIdWithStatusDto, Device>()
                .ReverseMap();

            CreateMap<NameAndIdWithStatusDto, NameAndIdWithStatusModel>()
                .ReverseMap();

            CreateMap<VideoChunkShortInfoDto, VideoChunk>()
                .ReverseMap();

            CreateMap<DeviceType, DeviceTypeDto>()
                .ReverseMap();

            CreateMap<DeviceType, CreateDeviceTypeDto>()
                .ReverseMap();

            CreateMap<DeviceTypeDto, DeviceTypeDisplayModel>()
                .ForMember(dest => dest.IconUrl, opt => opt.Ignore())
                .ReverseMap();

            CreateMap<CreatePreprovisionAttemptDto, PreprovisionAttempt>()
                .ReverseMap();

            CreateMap<PreprovisionAttemptDto, PreprovisionAttempt>()
                .ReverseMap();

            CreateMap<CameraStreamingConfigurationDto, CameraConfigurationModel>()
                .ReverseMap();
        }
    }
}
