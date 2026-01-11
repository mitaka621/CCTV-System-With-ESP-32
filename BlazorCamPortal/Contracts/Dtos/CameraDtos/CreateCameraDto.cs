using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Dtos.CameraDtos
{
    public class CreateCameraDto
    {
        public required string Ipv4Address { get; set; }

        public required string MacAddress { get; set; }

        public required PairStatus PairStatus { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
