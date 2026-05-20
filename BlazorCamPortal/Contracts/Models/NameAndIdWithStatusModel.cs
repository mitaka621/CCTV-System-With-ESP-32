using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Models
{
    public class NameAndIdWithStatusModel
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public DevicePairStatus Status { get; set; }
    }
}
