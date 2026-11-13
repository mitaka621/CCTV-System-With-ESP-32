using Homewatch.Contracts.Abstractions.UnitOfWork;
using Homewatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Homewatch.Infrastructure.UnitOfWork
{
    public sealed class UnitOfWorkFactory : IUnitOfWorkFactory
    {
        private readonly IDbContextFactory<HomewatchDBContext> _dbContextFactory;

        public UnitOfWorkFactory(IDbContextFactory<HomewatchDBContext> dbContextFactory)
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
