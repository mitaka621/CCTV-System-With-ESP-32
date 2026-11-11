using CamPortal.Contracts.Enums;
using MudBlazor;

namespace CamPortal.Components.Shared
{
    public static class DeviceCategoryAppearance
    {
        public static Color GetColor(DeviceTypeCategories category)
        {
            return category switch
            {
                DeviceTypeCategories.Camera => Color.Primary,
                DeviceTypeCategories.SmokeAlarm => Color.Secondary,
                _ => Color.Default,
            };
        }

        public static string GetIcon(DeviceTypeCategories category)
        {
            return category switch
            {
                DeviceTypeCategories.Camera => Icons.Material.Filled.Videocam,
                DeviceTypeCategories.SmokeAlarm => Icons.Material.Filled.LocalFireDepartment,
                _ => Icons.Material.Filled.DeviceHub,
            };
        }
    }
}
