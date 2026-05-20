namespace CamPortal.Contracts.Models
{
    public class PreprovisionVerificationModel
    {
        public Guid DeviceId { get; set; }

        public required string DeviceSignature { get; set; }
    }
}
