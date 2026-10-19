using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.RemoteStorageDtos;
using CamPortal.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace CamPortal.Core.Services.RemoteStorage
{
    public class SmbConnectionService : ISmbConnectionService
    {
        private readonly IDataProtector _passwordProtector;
        private readonly int _responseTimeoutInMilliseconds;

        public SmbConnectionService(IDataProtectionProvider dataProtectionProvider, IConfiguration configuration)
        {
            _passwordProtector = dataProtectionProvider.CreateProtector("CamPortal.RemoteStorageTarget.Password");

            _responseTimeoutInMilliseconds = (int)TimeSpan.FromSeconds(int.Parse(
                configuration.GetSection("RemoteStorageConfig")["ResponseTimeoutInSeconds"]
                ?? throw new ArgumentNullException("ResponseTimeoutInSeconds not configured"))).TotalMilliseconds;
        }

        public string ProtectPassword(string password)
        {
            return _passwordProtector.Protect(password);
        }

        public string UnprotectPassword(string protectedPassword)
        {
            if (string.IsNullOrEmpty(protectedPassword))
            {
                return string.Empty;
            }

            return _passwordProtector.Unprotect(protectedPassword);
        }

        public ISmbShareSession OpenSession(RemoteStorageTargetDto target)
        {
            return OpenSession(target.SharePath, target.Username, UnprotectPassword(target.ProtectedPassword));
        }

        public void VerifyWriteAccess(string sharePath, string username, string password)
        {
            using var session = OpenSession(sharePath, username, password);

            var testFileName = $".camportal-verify-{Guid.NewGuid():N}.tmp";

            session.CreateDirectory(string.Empty);

            using (var testContent = new MemoryStream(Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O"))))
            {
                session.UploadFile(testContent, testFileName);
            }

            session.DeleteFile(testFileName);
        }

        private ISmbShareSession OpenSession(string sharePath, string username, string password)
        {
            if (!SmbSharePathUtilities.TryParse(sharePath, out var smbSharePath))
            {
                throw new ArgumentException($@"{sharePath} is not a valid share path. Use the format \\server\share.");
            }

            return SmbShareSession.Open(smbSharePath, username, password, _responseTimeoutInMilliseconds);
        }
    }
}
