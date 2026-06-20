using CamPortal.Contracts.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace CamPortal.Core.Services.Provisioning
{
    public class ServerIdentityService : IServerIdentityService, IDisposable
    {
        private readonly ILogger<ServerIdentityService> _logger;
        private readonly ECDsa _ecdsa;
        private readonly string _publicKeySpkiBase64;

        public string PublicKeySpkiBase64 => _publicKeySpkiBase64;

        public ServerIdentityService(IConfiguration configuration, ILogger<ServerIdentityService> logger)
        {
            _logger = logger;

            var path = configuration.GetSection("ServerIdentity")["PrivateKeyPath"]
                ?? throw new ArgumentNullException("ServerIdentity:PrivateKeyPath not configured");

            _ecdsa = LoadOrGenerate(path);

            var spkiDer = _ecdsa.ExportSubjectPublicKeyInfo();
            _publicKeySpkiBase64 = Convert.ToBase64String(spkiDer);
        }

        private ECDsa LoadOrGenerate(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(fullPath))
            {
                try
                {
                    var pkcs8Pem = File.ReadAllText(fullPath);
                    var ecdsa = ECDsa.Create();
                    ecdsa.ImportFromPem(pkcs8Pem);

                    if (!IsP256(ecdsa))
                    {
                        ecdsa.Dispose();
                        throw new InvalidOperationException("Server identity key is not P-256");
                    }

                    _logger.LogInformation("Loaded server identity key from {Path}", fullPath);
                    return ecdsa;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load server identity key from {Path}; refusing to start", fullPath);
                    throw;
                }
            }

            _logger.LogWarning("Server identity key not found at {Path}; generating a new one", fullPath);

            var generated = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var pem = generated.ExportPkcs8PrivateKeyPem();
            File.WriteAllText(fullPath, pem);

            try
            {
                var fi = new FileInfo(fullPath);
                fi.Attributes |= FileAttributes.Hidden;
            }
            catch
            {
            }

            _logger.LogInformation("Generated new server identity key at {Path}", fullPath);
            return generated;
        }

        private static bool IsP256(ECDsa ecdsa)
        {
            var parameters = ecdsa.ExportParameters(false);
            return parameters.Curve.Oid.FriendlyName == ECCurve.NamedCurves.nistP256.Oid.FriendlyName
                || parameters.Curve.Oid.Value == ECCurve.NamedCurves.nistP256.Oid.Value;
        }

        public byte[] SignHashRawP1363(ReadOnlySpan<byte> hash)
        {
            return _ecdsa.SignHash(hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }

        public ECDiffieHellman CreateEphemeralEcdh()
        {
            return ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        }

        public void Dispose()
        {
            _ecdsa.Dispose();
        }
    }
}
