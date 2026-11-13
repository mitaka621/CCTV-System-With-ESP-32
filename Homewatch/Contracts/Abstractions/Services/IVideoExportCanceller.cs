namespace Homewatch.Contracts.Abstractions.Services
{
    public interface IVideoExportCanceller
    {
        void RequestCancel(Guid exportId);
    }
}
