namespace CamPortal.Contracts.Models
{
    public class CameraResponseModel : PreprovisionDeviceModel
    {
        public required string Hmac { get; set; }

        public string? Challenge { get; set; }
    }
}
