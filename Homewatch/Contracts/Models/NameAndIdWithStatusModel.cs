using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Models
{
    public class NameAndIdWithStatusModel
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public DevicePairStatus PairStatus { get; set; }
    }
}
