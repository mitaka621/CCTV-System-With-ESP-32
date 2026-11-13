using Homewatch.Contracts.Abstractions.UnitOfWork;
using Homewatch.Contracts.Dtos.PreprovisionAttemptDtos;
using Homewatch.Contracts.Enums;

namespace Homewatch.Contracts.Abstractions.Repositories
{
    public interface IPreprovisionAttemptRepository
    {
        Task<Guid> AddPreprovisionAttemptAsync(CreatePreprovisionAttemptDto attempt, IUnitOfWork? uow = null);

        Task<bool> FinishPreprovisionAttemptAsync(FinishPreprovisionAttemptDto dto, IUnitOfWork? uow = null);

        Task<bool> RevokePreprovisionAttemptsAsync(Guid deviceId, IUnitOfWork? uow = null);

        Task<bool> DecreaseRemainingAttemptsAndRevokeIfDepletedAsync(Guid preprovisionId);

        Task<PreprovisionAttemptDto?> GetPreprovisionAttemptNonceWithStatusAsync(Guid deviceId, PreprovisionStatus status);

        Task<PreprovisionAttemptDto?> GetLatestPreprovisionAttemptAsync(Guid deviceId);
    }
}
