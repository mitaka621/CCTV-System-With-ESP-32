using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Enums
{
    [Flags]
    public enum CameraMotionEvents
    {
        [Display(Name = "None")]
        None = 0,

        [Display(Name = "Movement")]
        Movement = 1,

        [Display(Name = "Impact")]
        Impact = 2,

        [Display(Name = "Fall")]
        Fall = 4,

        [Display(Name = "Rotation")]
        Rotation = 8
    }
}
