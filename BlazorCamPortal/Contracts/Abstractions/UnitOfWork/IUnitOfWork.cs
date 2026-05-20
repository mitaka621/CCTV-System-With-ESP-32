using Microsoft.EntityFrameworkCore;

namespace CamPortal.Contracts.Abstractions.UnitOfWork
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        DbContext Db { get; }

        Task CommitAsync(CancellationToken ct = default);

        Task RollbackAsync(CancellationToken ct = default);
    }
}
