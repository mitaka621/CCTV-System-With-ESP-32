using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Dtos.CameraDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.DeviceTypeDtos;
using CamPortal.Contracts.Dtos.PreprovisionAttemptDtos;
using CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos;
using CamPortal.Contracts.Dtos.UserCameraLayoutDtos;
using CamPortal.Contracts.Dtos.VideoChunkDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Models;
using CamPortal.Infrastructure.Data.Entities;
using Mapster;

namespace CamPortal.MapperConfiguration
{
    public class MapperRegister : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            RegisterTwoWayMap<Device, DeviceWithPreprovisionAttemptsDto>(config);

            RegisterTwoWayMap<Device, CreateDeviceDto>(config);

            RegisterTwoWayMap<CameraDisplayModel, DeviceWithPreprovisionAttemptsDto>(config);

            RegisterTwoWayMap<CameraResponseModel, CreateDeviceDto>(config);

            RegisterTwoWayMap<VideoChunk, CreateVideoChunkDto>(config);

            RegisterTwoWayMap<NameAndIdWithStatusDto, Device>(config);

            RegisterTwoWayMap<NameAndIdWithStatusDto, NameAndIdWithStatusModel>(config);

            RegisterTwoWayMap<VideoChunkShortInfoDto, VideoChunk>(config);

            RegisterTwoWayMap<DeviceType, DeviceTypeDto>(config);

            RegisterTwoWayMap<DeviceType, CreateDeviceTypeDto>(config);

            RegisterTwoWayMap<DeviceTypeDto, DeviceTypeDisplayModel>(config);

            RegisterTwoWayMap<CreatePreprovisionAttemptDto, PreprovisionAttempt>(config);

            RegisterTwoWayMap<PreprovisionAttemptDto, PreprovisionAttempt>(config);

            RegisterTwoWayMap<CameraStreamingConfigurationDto, CameraConfigurationModel>(config);

            RegisterTwoWayMap<CameraConfiguration, CameraStreamingConfigurationDto>(config);

            RegisterTwoWayMap<UserCameraPositionLayout, UserCameraLayoutItemDto>(config);

            RegisterTwoWayMap<SmokeAlarmConfigurationDto, SmokeAlarmDetectorConfiguration>(config);

            RegisterTwoWayMap<SmokeAlarmDetectorPayloadDto, SmokeAlarmTelemetry>(config);

            RegisterTwoWayMap<SmokeAlarmConfigurationDto, SmokeAlarmConfigurationModel>(config);

            RegisterTwoWayMap<CameraDisplayModel, DeviceDisplayModel>(config);

            RegisterTwoWayMap<SmokeAlarmDisplayModel, DeviceDisplayModel>(config);

            RegisterTwoWayMap<DeviceDto, Device>(config);

            RegisterTwoWayMap<DeviceDto, CameraDisplayModel>(config);

            config.NewConfig<CameraDto, CameraDisplayModel>()
                .Inherits<DeviceDto, CameraDisplayModel>()
                .Map(destination => destination.SensorResolution, source => source.CameraConfiguration != null ? source.CameraConfiguration.SensorResolution : (CameraSensorResolutions?)null);

            RegisterTwoWayMap<DeviceDto, SmokeAlarmDisplayModel>(config);

            RegisterTwoWayMap<SmokeAlarmShortPayloadDto, SmokeAlarmTelemetry>(config);

            RegisterTwoWayMap<DeviceStreamingHandshakeDto, DeviceStreamingHandshakeWithCameraConfigDto>(config);

        }

        private static void RegisterTwoWayMap<TFirst, TSecond>(TypeAdapterConfig config)
        {
            config.NewConfig<TFirst, TSecond>();
            config.NewConfig<TSecond, TFirst>();
        }
    }
}
