using System.Security.Cryptography;

namespace CamPortal.Contracts.Abstractions.Services
{
    public interface IServerIdentityService
    {
        string PublicKeySpkiBase64 { get; }

        byte[] SignHashRawP1363(ReadOnlySpan<byte> hash);

        ECDiffieHellman CreateEphemeralEcdh();
    }
}
