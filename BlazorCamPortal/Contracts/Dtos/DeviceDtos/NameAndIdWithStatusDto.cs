using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.CameraDtos
{
    public class NameAndIdWithStatusDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public DevicePairStatus PairStatus { get; set; }
    }
}
