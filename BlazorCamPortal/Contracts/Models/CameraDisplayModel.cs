using BlazorCamPortal.Contracts.Enums;

namespace BlazorCamPortal.Contracts.Models
{
    public class CameraDisplayModel
    {
        public string? Name { get; set; }

        public required string Ipv4Address { get; set; }

        public required string MacAddress { get; set; }

        public PairStatus PairStatus { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
