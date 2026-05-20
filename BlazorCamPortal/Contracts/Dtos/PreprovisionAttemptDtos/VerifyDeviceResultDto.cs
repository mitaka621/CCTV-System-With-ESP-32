namespace CamPortal.Contracts.Dtos.PreprovisionAttemptDtos
{
    public class VerifyDeviceResultDto
    {
        public bool IsValid { get; set; }

        public Guid PreprovisionAttemptId { get; set; }
    }
}
