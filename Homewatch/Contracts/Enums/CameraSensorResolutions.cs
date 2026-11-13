using System.ComponentModel.DataAnnotations;

namespace Homewatch.Contracts.Enums
{
    public enum CameraSensorResolutions
    {
        [Display(Name = "800 x 600")]
        Resolution800x600 = 1,

        [Display(Name = "1024 x 768")]
        Resolution1024x768 = 2,

        [Display(Name = "1280 x 720")]
        Resolution1280x720 = 3,

        [Display(Name = "1280 x 1024")]
        Resolution1280x1024 = 4,

        [Display(Name = "1600 x 1200")]
        Resolution1600x1200 = 5,

        [Display(Name = "1920 x 1080")]
        Resolution1920x1080 = 6,

        [Display(Name = "2048 x 1536")]
        Resolution2048x1536 = 7,

        [Display(Name = "2560 x 1440")]
        Resolution2560x1440 = 8,

        [Display(Name = "2560 x 1600")]
        Resolution2560x1600 = 9,

        [Display(Name = "2560 x 1920")]
        Resolution2560x1920 = 10,

        [Display(Name = "720 x 1280 (portrait)")]
        Resolution720x1280 = 11,

        [Display(Name = "864 x 1536 (portrait)")]
        Resolution864x1536 = 12,

        [Display(Name = "1080 x 1920 (portrait)")]
        Resolution1080x1920 = 13
    }
}
