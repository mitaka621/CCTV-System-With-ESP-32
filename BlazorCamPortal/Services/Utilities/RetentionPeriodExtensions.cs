using CamPortal.Contracts.Enums;

namespace CamPortal.Core.Utilities
{
    public static class RetentionPeriodExtensions
    {
        public static TimeSpan? ToTimeSpan(this RetentionPeriod retentionPeriod)
        {
            return retentionPeriod switch
            {
                RetentionPeriod.OneDay => TimeSpan.FromDays(1),
                RetentionPeriod.ThreeDays => TimeSpan.FromDays(3),
                RetentionPeriod.OneWeek => TimeSpan.FromDays(7),
                RetentionPeriod.TwoWeeks => TimeSpan.FromDays(14),
                RetentionPeriod.OneMonth => TimeSpan.FromDays(30),
                RetentionPeriod.ThreeMonths => TimeSpan.FromDays(90),
                RetentionPeriod.SixMonths => TimeSpan.FromDays(180),
                RetentionPeriod.OneYear => TimeSpan.FromDays(365),
                _ => null
            };
        }
    }
}
