using CamPortal.Contracts.Dtos.LocalNetworkDtos;
using System.Net;
using System.Net.NetworkInformation;

namespace CamPortal.Core.Utilities
{
    public static class NetworkUtilities
    {
        private static List<LocalNetworkInfoDto>? _cachedNetworkInfo;
        private static DateTime _cacheExpiresTimestamp;

        public static bool IsClientInSameSubnet(IPAddress? client, string? networkIpv4, string? subnetMask)
        {
            if (client == null || string.IsNullOrWhiteSpace(networkIpv4) || string.IsNullOrWhiteSpace(subnetMask))
                return false;

            if (!IPAddress.TryParse(networkIpv4, out var network))
                return false;

            if (!IPAddress.TryParse(subnetMask, out var mask))
                return false;

            if (client.IsIPv4MappedToIPv6)
                client = client.MapToIPv4();

            if (client.AddressFamily != network.AddressFamily || client.AddressFamily != mask.AddressFamily)
                return false;

            var clientBytes = client.GetAddressBytes();
            var networkBytes = network.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();

            if (clientBytes.Length != networkBytes.Length || clientBytes.Length != maskBytes.Length)
                return false;

            for (int i = 0; i < clientBytes.Length; i++)
            {
                if ((clientBytes[i] & maskBytes[i]) != (networkBytes[i] & maskBytes[i]))
                    return false;
            }

            return true;
        }

        public static List<LocalNetworkInfoDto> GetLocalNetworkInfo()
        {
            if (_cachedNetworkInfo != null && DateTime.UtcNow < _cacheExpiresTimestamp)
            {
                return _cachedNetworkInfo;
            }

            var networkInfoList = new List<LocalNetworkInfoDto>();

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
                        networkInfoList.Add(new LocalNetworkInfoDto()
                        {
                            LocalIp = ua.Address.ToString(),
                            SubnetMask = ua.IPv4Mask.ToString(),
                            Gateway = gw?.ToString() ?? string.Empty
                        });
                    }
                }
            }

            if (networkInfoList.Count == 0)
                throw new Exception("No active IPv4 network adapters found.");

            _cachedNetworkInfo = networkInfoList;
            _cacheExpiresTimestamp = DateTime.UtcNow.AddHours(1);

            return networkInfoList;
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
