using BlazorCamPortal.Contracts.Enums;

namespace BlazorCamPortal.Contracts.Models
{
    public class NameAndIdWithStatusModel
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public PairStatus Status { get; set; }
    }
}
