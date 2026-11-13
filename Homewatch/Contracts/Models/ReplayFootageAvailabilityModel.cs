namespace Homewatch.Contracts.Models
{
    public class ReplayFootageAvailabilityModel
    {
        public Guid LocationId { get; set; }

        public required string LocationName { get; set; }

        public double AvailabilityPercentage { get; set; }
    }
}
