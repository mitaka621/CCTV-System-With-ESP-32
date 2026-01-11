namespace CamPortal.Contracts.Models
{
    public class CameraResponseModel : CreateCameraModel
    {
        public required string Hmac { get; set; }

        public string? Challenge { get; set; }
    }
}
