using System.ComponentModel.DataAnnotations;

namespace Homewatch.Contracts.Enums
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
