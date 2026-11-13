using Homewatch.Contracts.Dtos.LocalNetworkDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Dtos.DeviceDtos
{
    public class ResumeDeviceSetupDto
    {
        public Guid DeviceId { get; set; }

        public Guid DeviceTypeId { get; set; }

        public DeviceTypeCategories DeviceCategory { get; set; }

        public DevicePairStatus PairStatus { get; set; }

        public LocalNetworkInfoDto? LocalNetworkInfo { get; set; }
    }
}
