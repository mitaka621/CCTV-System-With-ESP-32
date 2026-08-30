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

        [Range(1, 5.5, ErrorMessage = "ChargeSenseVoltageThreashold must be between 1 and 5.5.")]
        [Label("Charge Sense Voltage Threashold")]
        public double ChargeSenseVoltageThreashold { get; set; }

        [Range(4, 5, ErrorMessage = "MaxVoltageOverchargeWarning must be between 4 and 5.")]
        [Label("Max battery voltage for overcharge warning")]
        public double MaxVoltageOverchargeWarning { get; set; }
    }
}
