using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Enums
{
    public enum DeviceTypeCategories
    {
        [Display(Name = "Camera")]
        Camera,

        [Display(Name = "Smoke Alarm")]
        SmokeAlarm,
    }
}
