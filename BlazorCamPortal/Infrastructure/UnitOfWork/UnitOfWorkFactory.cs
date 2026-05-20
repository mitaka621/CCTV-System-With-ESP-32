using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CamPortal.Infrastructure.UnitOfWork
{
    public sealed class UnitOfWorkFactory : IUnitOfWorkFactory
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;

        public UnitOfWorkFactory(IDbContextFactory<CamPortalDBContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<IUnitOfWork> CreateAsync(bool useTransaction = false, CancellationToken ct = default)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            IDbContextTransaction? transaction = useTransaction
                ? await dbContext.Database.BeginTransactionAsync(ct)
                : null;

            return new UnitOfWork(dbContext, transaction);
        }
    }
}
