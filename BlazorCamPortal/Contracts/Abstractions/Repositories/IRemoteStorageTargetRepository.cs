using CamPortal.Contracts.Dtos.RemoteStorageDtos;

namespace CamPortal.Contracts.Abstractions.Repositories
{
    public interface IRemoteStorageTargetRepository
    {
        Task<List<RemoteStorageTargetDto>> GetTargetsAsync();

        Task<Dictionary<Guid, RemoteStorageTargetDto>> GetEnabledAsync();

        Task<RemoteStorageTargetDto?> GetTargetAsync(Guid targetId);

        Task<bool> DoesSharePathExistAsync(string path, params List<Guid> targetIdsToExcludeInSerach);

        Task SaveTargetAsync(RemoteStorageTargetDto target);

        Task UpdateTargetAsync(RemoteStorageTargetDto target);
    }
}
