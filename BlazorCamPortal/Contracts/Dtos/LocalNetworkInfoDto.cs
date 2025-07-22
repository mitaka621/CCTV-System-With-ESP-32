using System.Net;

namespace BlazorCamPortal.Contracts.Dtos
{
    public class LocalNetworkInfoDto
    {
        public required IPAddress LocalIp { get; set; }

        public required IPAddress SubnetMask { get; set; }

        public required IPAddress Gateway { get; set; }
    }
}
