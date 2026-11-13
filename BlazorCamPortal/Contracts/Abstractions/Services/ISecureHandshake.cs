using Homewatch.Contracts.Dtos.DeviceDtos;
using System.Net;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface ISecureHandshake
    {
        Task<DeviceStreamingHandshakeDto?> AuthorizeAsync(Stream stream, EndPoint? remoteEndpoint, CancellationToken cancellationToken);

        Task<ISecureChannel?> EstablishAsync(Stream stream, DeviceStreamingHandshakeDto device, CancellationToken cancellationToken);
    }
}
