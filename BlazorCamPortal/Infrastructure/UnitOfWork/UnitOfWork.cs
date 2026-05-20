using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CamPortal.Infrastructure.UnitOfWork
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly CamPortalDBContext _dbContext;
        private readonly IDbContextTransaction? _transaction;

        public UnitOfWork(CamPortalDBContext dbContext, IDbContextTransaction? transaction)
        {
            _dbContext = dbContext;
            _transaction = transaction;
        }

        public DbContext Db => _dbContext;

        public async Task CommitAsync(CancellationToken ct = default)
        {
            await _dbContext.SaveChangesAsync(ct);

            if (_transaction != null)
            {
                await _transaction.CommitAsync(ct);
            }
        }

        public Task RollbackAsync(CancellationToken ct = default)
        {
            return _transaction?.RollbackAsync(ct) ?? Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
            }

            await _dbContext.DisposeAsync();
        }
    }
}
