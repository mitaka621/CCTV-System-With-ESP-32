using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Dtos.RemoteStorageDtos
{
    public class RemoteStorageTargetDto
    {
        public Guid Id { get; set; }

        public string SharePath { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string ProtectedPassword { get; set; } = string.Empty;

        public bool IsSyncEnabled { get; set; }

        public RetentionPeriod Retention { get; set; }
    }
}
