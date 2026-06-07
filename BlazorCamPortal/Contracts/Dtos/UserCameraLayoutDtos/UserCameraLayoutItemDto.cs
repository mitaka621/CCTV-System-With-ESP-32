using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.UserCameraLayoutDtos
{
    public class UserCameraLayoutItemDto
    {
        public CameraInfoWithConfigurationDto? CameraInfo { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public CameraLayoutType LayoutType { get; set; }
    }
}
