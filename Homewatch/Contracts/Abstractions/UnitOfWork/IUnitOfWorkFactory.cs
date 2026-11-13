namespace Homewatch.Contracts.Abstractions.UnitOfWork
{
    public interface IUnitOfWorkFactory
    {
        Task<IUnitOfWork> CreateAsync(bool useTransaction = false, CancellationToken ct = default);
    }
}
