namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IVideoExportCanceller
    {
        void RequestCancel(Guid exportId);
    }
}
