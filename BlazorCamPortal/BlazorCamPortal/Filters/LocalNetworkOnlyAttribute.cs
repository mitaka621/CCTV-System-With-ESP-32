using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;
using System.Net.Sockets;

namespace CamPortal.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class LocalNetworkOnlyAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var ip = context.HttpContext.Connection.RemoteIpAddress;

            if (ip == null)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }

            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            if (!IsLocal(ip))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            }
        }

        private static bool IsLocal(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip))
            {
                return true;
            }

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = ip.GetAddressBytes();

                if (bytes[0] == 10)
                {
                    return true;
                }

                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                {
                    return true;
                }

                if (bytes[0] == 192 && bytes[1] == 168)
                {
                    return true;
                }

                if (bytes[0] == 169 && bytes[1] == 254)
                {
                    return true;
                }
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
                {
                    return true;
                }

                var first = ip.GetAddressBytes()[0];
                if ((first & 0xFE) == 0xFC)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
