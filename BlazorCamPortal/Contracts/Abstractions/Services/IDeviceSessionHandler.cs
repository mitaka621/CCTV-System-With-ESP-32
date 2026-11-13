using Homewatch.Contracts.Dtos.DeviceDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IDeviceSessionHandler
    {
        DeviceTypeCategories DeviceCategory { get; }

        Task RunRecieveLoopAsync(ISecureChannel secureChannel, DeviceStreamingHandshakeDto device, CancellationToken cancellationToken);

        Task RunSendLoopAsync(ISecureChannel secureChannel, DeviceStreamingHandshakeDto device, CancellationToken cancellationToken);
    }
}
