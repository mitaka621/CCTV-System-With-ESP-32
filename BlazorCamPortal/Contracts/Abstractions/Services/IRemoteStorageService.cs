using Homewatch.Contracts.Dtos.RemoteStorageDtos;
using Homewatch.Contracts.Models;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IRemoteStorageService
    {
        Task<List<RemoteStorageTargetModel>> GetTargetsAsync();

        Task<RemoteStorageTargetDto?> GetEnabledTargetAsync(Guid remoteTargetId);

        Task<RemoteStorageOperationResultDto> SaveTargetAsync(RemoteStorageTargetModel target);

        Task<RemoteStorageOperationResultDto> VerifyTargetAsync(RemoteStorageTargetModel target);
    }
}
