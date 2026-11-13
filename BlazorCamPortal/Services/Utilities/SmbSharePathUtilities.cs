using Homewatch.Core.Services.RemoteStorage;

namespace Homewatch.Core.Utilities
{
    public static class SmbSharePathUtilities
    {
        public static bool TryParse(string? sharePath, out SmbSharePathDto smbSharePath)
        {
            smbSharePath = null!;

            if (string.IsNullOrWhiteSpace(sharePath))
            {
                return false;
            }

            var trimmedSharePath = sharePath.Trim();

            if (trimmedSharePath.StartsWith("smb://", StringComparison.OrdinalIgnoreCase))
            {
                trimmedSharePath = trimmedSharePath["smb://".Length..];
            }
            else if (trimmedSharePath.StartsWith(@"\\", StringComparison.Ordinal) || trimmedSharePath.StartsWith("//", StringComparison.Ordinal))
            {
                trimmedSharePath = trimmedSharePath[2..];
            }
            else
            {
                return false;
            }

            var segments = trimmedSharePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length < 2)
            {
                return false;
            }

            string server = segments[0];
            string shareName = segments[1];
            string baseFolder = string.Join('\\', segments.Skip(2));

            smbSharePath = new SmbSharePathDto()
            {
                BaseFolder = baseFolder,
                Server = server,
                ShareName = shareName
            };

            return true;
        }
    }
}
