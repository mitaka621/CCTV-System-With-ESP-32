using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Models
{
    public class CameraDisplayModel
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }

        public required string Ipv4Address { get; set; }

        public required string MacAddress { get; set; }

        public DevicePairStatus PairStatus { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
