using BlazorCamPortal.Contracts.Enums;

namespace BlazorCamPortal.Contracts.Dtos.CameraDtos
{
    public class CreateCameraDto
    {
        public required string Ipv4Address { get; set; }

        public required string MacAddress { get; set; }

        public required PairStatus PairStatus { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
