using System.ComponentModel.DataAnnotations;

namespace CamPortal.Contracts.Enums
{
    public enum RetentionPeriod
    {
        [Display(Name = "Never")]
        Never,

        [Display(Name = "1 Day")]
        OneDay,

        [Display(Name = "3 Days")]
        ThreeDays,

        [Display(Name = "1 Week")]
        OneWeek,

        [Display(Name = "2 Weeks")]
        TwoWeeks,

        [Display(Name = "1 Month")]
        OneMonth,

        [Display(Name = "3 Months")]
        ThreeMonths,

        [Display(Name = "6 Months")]
        SixMonths,

        [Display(Name = "1 Year")]
        OneYear
    }
}
