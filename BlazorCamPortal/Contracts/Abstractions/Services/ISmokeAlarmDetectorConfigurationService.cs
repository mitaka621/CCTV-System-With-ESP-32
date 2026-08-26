using CamPortal.Contracts.Models;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ISmokeAlarmDetectorConfigurationService
    {
        Task<bool> UpdateConfigurationAsync(SmokeAlarmConfigurationModel model);

        Task<SmokeAlarmConfigurationModel> GetSmokeAlarmConfigurationAsync(Guid deviceId);
    }
}
