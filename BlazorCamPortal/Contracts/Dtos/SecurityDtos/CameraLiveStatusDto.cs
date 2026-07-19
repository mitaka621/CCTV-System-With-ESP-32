namespace CamPortal.Contracts.Dtos.SecurityDtos
{
    public class CameraLiveStatusDto
    {
        public Guid CameraId { get; set; }

        public bool Online { get; set; }

        public bool SecurityArmed { get; set; }

        public bool AlarmActive { get; set; }

        public bool Warning { get; set; }

        public string? WarningReason { get; set; }
    }
}
