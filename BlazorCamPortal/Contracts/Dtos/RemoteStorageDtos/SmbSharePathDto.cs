namespace CamPortal.Core.Services.RemoteStorage
{
    public class SmbSharePathDto
    {
        public required string Server { get; init; }

        public required string ShareName { get; init; }

        public required string BaseFolder { get; init; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(BaseFolder)
                ? $@"\\{Server}\{ShareName}"
                : $@"\\{Server}\{ShareName}\{BaseFolder}";
        }
    }
}
