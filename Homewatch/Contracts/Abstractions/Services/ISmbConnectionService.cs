using Homewatch.Contracts.Dtos.RemoteStorageDtos;

namespace Homewatch.Contracts.Abstractions.Services
{
    public interface ISmbConnectionService
    {
        string ProtectPassword(string password);

        string UnprotectPassword(string protectedPassword);

        ISmbShareSession OpenSession(RemoteStorageTargetDto target);

        void VerifyWriteAccess(string sharePath, string username, string password);
    }
}
