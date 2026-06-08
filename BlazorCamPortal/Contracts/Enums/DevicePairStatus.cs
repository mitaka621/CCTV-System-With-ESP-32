using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Enums
{
    public enum DevicePairStatus
    {
        [Display(Name = "Pairing")]
        PairingPending,

        [Display(Name = "Paired")]
        Paired,

        [Display(Name = "Forgotten")]
        Removed
    }
}
