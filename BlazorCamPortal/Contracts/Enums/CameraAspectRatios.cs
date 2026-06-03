using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Enums
{
    public enum CameraAspectRatios
    {
        [Display(Name = "Original")]
        Original,

        [Display(Name = "4:3")]
        Ratio4_3,

        [Display(Name = "3:4")]
        Ratio3_4,

        [Display(Name = "16:9")]
        Ratio16_9,

        [Display(Name = "9:16")]
        Ratio9_16
    }
}
