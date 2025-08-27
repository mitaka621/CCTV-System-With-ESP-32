using BlazorCamPortal.Contracts.Enums;

namespace BlazorCamPortal.Contracts.Dtos.CameraDtos
{
    public class NameAndIdWithStatusDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public PairStatus Status { get; set; }
    }
}
