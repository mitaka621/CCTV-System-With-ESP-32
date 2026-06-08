using CamPortal.Contracts.Dtos.LocalNetworkDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.DeviceDtos
{
    public class ResumeDeviceSetupDto
    {
        public Guid DeviceId { get; set; }

        public Guid DeviceTypeId { get; set; }

        public DevicePairStatus PairStatus { get; set; }

        public LocalNetworkInfoDto? LocalNetworkInfo { get; set; }
    }
}
