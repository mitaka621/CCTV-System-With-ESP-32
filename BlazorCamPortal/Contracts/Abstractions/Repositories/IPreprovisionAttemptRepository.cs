using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.PreprovisionAttemptDtos;
using CamPortal.Contracts.Enums;

namespace CamPortal.Contracts.Abstractions.Repositories
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
