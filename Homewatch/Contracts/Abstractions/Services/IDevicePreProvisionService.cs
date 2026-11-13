using Homewatch.Contracts.Dtos.DeviceDtos;
using Homewatch.Contracts.Dtos.PreprovisionAttemptDtos;
using Homewatch.Contracts.Models;
using System.Net;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IDevicePreProvisionService
    {
        Task<PreprovisionDetailsDto?> StartPreprovisioningAsync(PreprovisionDeviceModel model);

        Task<PreprovisionDetailsDto?> StartPreprovisioningForExistingDeviceAsync(Guid deviceId, PreprovisionDeviceModel model);

        Task<VerifyDeviceResultDto> VerifyDeviceAsync(IPAddress? deviceAddress, PreprovisionVerificationModel model);

        Task<bool> FinishPreprovisioningAsync(FinishPreprovisionAttemptDto dto);

        Task<bool> CancelPairingAndDeleteDeviceAsync(Guid deviceId);

        Task<ResumeDeviceSetupDto?> GetResumeSetupStateAsync(Guid deviceId);
    }
}
