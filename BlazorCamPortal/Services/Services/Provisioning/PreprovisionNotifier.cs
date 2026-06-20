using CamPortal.Contracts.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace CamPortal.Core.Services.Provisioning
{
    public class PreprovisionNotifier : IPreprovisionNotifier
    {
        private readonly ILogger<PreprovisionNotifier> _logger;

        public event Action<Guid>? SuccessfulVerification;

        public PreprovisionNotifier(ILogger<PreprovisionNotifier> logger)
        {
            _logger = logger;
        }

        public void NotifySuccessfulVerification(Guid deviceId)
        {
            var handlers = SuccessfulVerification;
            if (handlers == null)
            {
                _logger.LogDebug("SuccessfulVerification raised for device {DeviceId} but no subscribers", deviceId);
                return;
            }

            foreach (var handler in handlers.GetInvocationList().Cast<Action<Guid>>())
            {
                try
                {
                    handler(deviceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SuccessfulVerification handler threw for device {DeviceId}", deviceId);
                }
            }
        }
    }
}
