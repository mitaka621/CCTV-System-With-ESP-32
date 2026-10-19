using CamPortal.Contracts.Dtos.RemoteStorageDtos;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface ISmbConnectionService
    {
        string ProtectPassword(string password);

        string UnprotectPassword(string protectedPassword);

        ISmbShareSession OpenSession(RemoteStorageTargetDto target);

        void VerifyWriteAccess(string sharePath, string username, string password);
    }
}
