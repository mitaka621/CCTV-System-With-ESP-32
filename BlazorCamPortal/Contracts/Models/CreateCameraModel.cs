namespace CamPortal.Contracts.Models
{
    public class CreateCameraModel
    {
        public string? Ipv4Address { get; set; }

        public required string MacAddress { get; set; }
    }
}
