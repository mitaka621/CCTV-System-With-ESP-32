namespace CamPortal.Contracts.Dtos.SecureStreamingDtos
{
    public class SecureSessionMaterialDto
    {
        public required byte[] SessionKey { get; init; }

        public required byte[] IvBase { get; init; }

        public required byte[] DownstreamKey { get; init; }

        public required byte[] DownstreamIvBase { get; init; }

        public required byte[] SessionId { get; init; }

        public required byte[] DeviceIdBytes { get; init; }

        public required DateTime StartedAt { get; init; }
    }
}
