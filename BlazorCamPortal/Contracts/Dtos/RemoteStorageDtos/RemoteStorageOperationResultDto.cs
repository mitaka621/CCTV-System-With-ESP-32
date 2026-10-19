namespace CamPortal.Contracts.Dtos.RemoteStorageDtos
{
    public class RemoteStorageOperationResultDto
    {
        public Guid? TargetId { get; set; }

        public bool Succeeded { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
