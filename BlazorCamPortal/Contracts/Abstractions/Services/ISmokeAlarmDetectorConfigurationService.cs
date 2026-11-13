using Homewatch.Contracts.Models;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface ISmokeAlarmDetectorConfigurationService
    {
        Task<bool> UpdateConfigurationAsync(SmokeAlarmConfigurationModel model);

        Task<SmokeAlarmConfigurationModel> GetSmokeAlarmConfigurationAsync(Guid deviceId);
    }
}
