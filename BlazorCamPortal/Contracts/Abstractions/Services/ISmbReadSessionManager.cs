using CamPortal.Contracts.Dtos.RemoteStorageDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ISmbReadSessionManager
    {
        Task<byte[]?> ReadFileAsync(RemoteStorageTargetDto target, string relativePath, CancellationToken cancellationToken);
    }
}
