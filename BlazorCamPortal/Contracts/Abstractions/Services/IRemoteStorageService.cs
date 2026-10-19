using CamPortal.Contracts.Dtos.RemoteStorageDtos;
using CamPortal.Contracts.Models;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IRemoteStorageService
    {
        Task<List<RemoteStorageTargetModel>> GetTargetsAsync();

        Task<RemoteStorageTargetDto?> GetEnabledTargetAsync(Guid remoteTargetId);

        Task<RemoteStorageOperationResultDto> SaveTargetAsync(RemoteStorageTargetModel target);

        Task<RemoteStorageOperationResultDto> VerifyTargetAsync(RemoteStorageTargetModel target);
    }
}
