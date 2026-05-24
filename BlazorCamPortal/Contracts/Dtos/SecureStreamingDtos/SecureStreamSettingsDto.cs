namespace CamPortal.Contracts.Dtos.SecureStreamingDtos
{
    public class SecureStreamSettingsDto
    {
        public int HandshakeTimeoutSeconds { get; set; }
        public int FrameReadTimeoutSeconds { get; set; }
        public int MaxFrameBytes { get; set; }
        public int ReplayWindow { get; set; }
        public int MaxSessionDurationMinutes { get; set; }
        public long MaxSessionFrames { get; set; }
    }
}
