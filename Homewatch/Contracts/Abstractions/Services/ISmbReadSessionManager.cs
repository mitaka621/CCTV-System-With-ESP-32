using Homewatch.Contracts.Dtos.RemoteStorageDtos;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface ISmbReadSessionManager
    {
        Task<byte[]?> ReadFileAsync(RemoteStorageTargetDto target, string relativePath, CancellationToken cancellationToken);
    }
}
