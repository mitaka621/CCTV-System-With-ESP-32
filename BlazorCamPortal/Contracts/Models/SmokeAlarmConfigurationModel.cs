using MudBlazor;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Models
{
    public class SmokeAlarmConfigurationModel
    {
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Device name must be between 1 and 100 characters.")]
        [Label("Device Name")]
        public string DeviceName { get; set; } = string.Empty;

        public Guid DeviceId { get; set; }

        [Range(1, 99, ErrorMessage = "Minimum battery SoC for alert must be between 1 and 99.")]
        [Label("Minimum Battery SoC for Alert")]
        public double MinBatterySOCForAlert { get; set; } = 10;
    }
}
