using System.Net;
using System.Net.NetworkInformation;
using BlazorCamPortal.Contracts.Dtos;

namespace BlazorCamPortal.Core.Utilities
{
    public static class NetworkUtilites
    {
        public static LocalNetworkInfoDto GetLocalNetworkInfo()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                var ipProps = ni.GetIPProperties();
                var gw = ipProps.GatewayAddresses.FirstOrDefault()?.Address;

                foreach (var ua in ipProps.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return new LocalNetworkInfoDto()
                        {
                            LocalIp = ua.Address,
                            SubnetMask = ua.IPv4Mask,
                            Gateway = gw!
                        };
                    }
                }
            }

            throw new Exception("No active IPv4 network adapters found.");
        }

        public static int GetNumberOfUsableHosts(IPAddress subnetMask)
        {
            uint mask = BitConverter.ToUInt32(subnetMask.GetAddressBytes().Reverse().ToArray(), 0);
            int bits = CountBits(mask);
            int hostBits = 32 - bits;
            int usableHosts = (int)Math.Pow(2, hostBits) - 2;
            return usableHosts;
        }

        private static int CountBits(uint value)
        {
            int count = 0;
            while (value != 0)
            {
                count += (int)(value & 1);
                value >>= 1;
            }
            return count;
        }
    }
}
