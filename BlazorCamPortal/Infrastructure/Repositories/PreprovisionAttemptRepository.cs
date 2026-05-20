using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.PreprovisionAttemptDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Infrastructure.Data;
using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class PreprovisionAttemptRepository : IPreprovisionAttemptRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public PreprovisionAttemptRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task<Guid> AddPreprovisionAttemptAsync(CreatePreprovisionAttemptDto attempt, IUnitOfWork? uow = null)
        {
            var entity = _mapper.Map<PreprovisionAttempt>(attempt);

            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.NewGuid();
            }

            if (uow != null)
            {
                var ownedDb = (CamPortalDBContext)uow.Db;
                ownedDb.PreprovisionAttempts.Add(entity);

                return entity.Id;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            db.PreprovisionAttempts.Add(entity);

            await db.SaveChangesAsync();

            return entity.Id;
        }

        public async Task<bool> FinishPreprovisionAttemptAsync(FinishPreprovisionAttemptDto dto, IUnitOfWork? uow = null)
        {
            if (uow != null)
            {
                var ownedDb = (CamPortalDBContext)uow.Db;

                return await ownedDb.PreprovisionAttempts
                    .Where(a => a.Id == dto.PreprovisionAttemptId && a.PreprovisionStatus == PreprovisionStatus.Pending)
                    .ExecuteUpdateAsync(x => x.SetProperty(a => a.ClaimedAt, dto.ClaimedAt)
                        .SetProperty(a => a.ClaimedFromIpv4, dto.ClaimedFromIpv4)
                        .SetProperty(a => a.PreprovisionStatus, dto.PreprovisionStatus)) > 0;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.PreprovisionAttempts
                .Where(a => a.Id == dto.PreprovisionAttemptId && a.PreprovisionStatus == PreprovisionStatus.Pending)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.ClaimedAt, dto.ClaimedAt)
                    .SetProperty(a => a.ClaimedFromIpv4, dto.ClaimedFromIpv4)
                    .SetProperty(a => a.PreprovisionStatus, dto.PreprovisionStatus)) > 0;
        }

        public async Task<bool> RevokePreprovisionAttemptsAsync(Guid deviceId, IUnitOfWork? uow = null)
        {
            if (uow != null)
            {
                var ownedDb = (CamPortalDBContext)uow.Db;

                return await ownedDb.PreprovisionAttempts
                    .Where(a => a.DeviceId == deviceId && a.PreprovisionStatus == PreprovisionStatus.Pending)
                    .ExecuteUpdateAsync(x => x.SetProperty(a => a.PreprovisionStatus, PreprovisionStatus.Revoked)) > 0;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.PreprovisionAttempts
                .Where(a => a.DeviceId == deviceId && a.PreprovisionStatus == PreprovisionStatus.Pending)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.PreprovisionStatus, PreprovisionStatus.Revoked)) > 0;
        }

        public async Task<PreprovisionAttemptDto?> GetPreprovisionAttemptNonceWithStatusAsync(Guid deviceId, PreprovisionStatus status)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var entity = await db.PreprovisionAttempts
                .AsNoTracking()
                .Where(a => a.DeviceId == deviceId && a.PreprovisionStatus == status)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            return _mapper.Map<PreprovisionAttemptDto?>(entity);
        }

        public async Task<bool> DecreaseRemainingAttemptsAndRevokeIfDepletedAsync(Guid preprovisionId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var decremented = await db.PreprovisionAttempts
                .Where(a => a.Id == preprovisionId
                         && a.PreprovisionStatus == PreprovisionStatus.Pending
                         && a.RemainingAttempts > 0)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.RemainingAttempts, a => a.RemainingAttempts - 1));

            if (decremented == 0) return false;

            await db.PreprovisionAttempts
                .Where(a => a.Id == preprovisionId && a.RemainingAttempts <= 0)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.PreprovisionStatus, PreprovisionStatus.Revoked));

            return true;
        }
    }
}
