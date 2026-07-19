namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ISecureChannel : IAsyncDisposable
    {
        Task<byte[]> ReceiveAsync(CancellationToken cancellationToken);

        Task SendAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken);
    }
}
