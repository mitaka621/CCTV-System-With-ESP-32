using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IDeviceSessionHandler
    {
        DeviceTypeCategories DeviceCategory { get; }

        Task RunRecieveLoopAsync(ISecureChannel secureChannel, DeviceStreamingHandshakeDto device, CancellationToken cancellationToken);

        Task RunSendLoopAsync(ISecureChannel secureChannel, DeviceStreamingHandshakeDto device, CancellationToken cancellationToken);
    }
}
