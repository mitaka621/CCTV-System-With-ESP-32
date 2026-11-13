using Homewatch.Contracts.Dtos.CameraConfigurationDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Dtos.UserCameraLayoutDtos
{
    public class UserCameraLayoutItemDto
    {
        public CameraInfoWithConfigurationDto? CameraInfo { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public CameraLayoutType LayoutType { get; set; }
    }
}
