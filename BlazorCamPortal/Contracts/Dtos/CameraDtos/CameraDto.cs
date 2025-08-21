using BlazorCamPortal.Contracts.Enums;

namespace BlazorCamPortal.Contracts.Dtos.CameraDtos
{
    public class CameraDto
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }

        public required string Ipv4Address { get; set; }

        public required string MacAddress { get; set; }

        public PairStatus PairStatus { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
