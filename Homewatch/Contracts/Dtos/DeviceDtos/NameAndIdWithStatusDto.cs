using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Dtos.CameraDtos
{
    public class NameAndIdWithStatusDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public DevicePairStatus PairStatus { get; set; }
    }
}
