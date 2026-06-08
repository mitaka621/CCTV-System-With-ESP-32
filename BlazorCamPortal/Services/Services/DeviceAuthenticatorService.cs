using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.DeviceDtos;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace CamPortal.Core.Services
{
    public class DeviceAuthenticatorService : IDeviceAuthenticatorService
    {
        private const int _nonceLengthBytes = 32;
        private const string _domainTag = "campr-provision-v1";

        private readonly ILogger<DeviceAuthenticatorService> _logger;

        public DeviceAuthenticatorService(ILogger<DeviceAuthenticatorService> logger)
        {
            _logger = logger;
        }

        public DeviceKeyPairDto GenerateKeyPair()
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var privateKeyDer = ecdsa.ExportPkcs8PrivateKey();
            var publicKeyDer = ecdsa.ExportSubjectPublicKeyInfo();
            var fingerprint = SHA256.HashData(publicKeyDer);

            return new DeviceKeyPairDto()
            {
                PrivateKeyBase64 = Convert.ToBase64String(privateKeyDer),
                PublicKeySpkiBase64 = Convert.ToBase64String(publicKeyDer),
                PublicKeyFingerprintHex = Convert.ToHexString(fingerprint).ToLowerInvariant()
            };

        }

        public string GenerateNonceBase64()
        {
            var nonce = RandomNumberGenerator.GetBytes(_nonceLengthBytes);
            return Convert.ToBase64String(nonce);
        }

        public byte[] ComputeBindingHash(
            Guid deviceId,
            string publicKeyFingerprintHex,
            string nonceBase64)
        {
            var header = Encoding.UTF8.GetBytes(
                $"{_domainTag}|{deviceId:N}|{publicKeyFingerprintHex}|");

            var nonce = Convert.FromBase64String(nonceBase64);

            using var sha = SHA256.Create();

            sha.TransformBlock(header, 0, header.Length, null, 0);

            sha.TransformFinalBlock(nonce, 0, nonce.Length);

            return sha.Hash!;
        }

        public bool VerifySignature(
            string publicKeySpkiBase64,
            byte[] bindingHash,
            string signatureBase64)
        {
            try
            {
                var publicKeyDer = Convert.FromBase64String(publicKeySpkiBase64);
                var signature = Convert.FromBase64String(signatureBase64);

                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(publicKeyDer, out _);

                return ecdsa.VerifyHash(
                    bindingHash,
                    signature,
                    DSASignatureFormat.Rfc3279DerSequence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during VerifySignature");
                return false;
            }
        }
    }
}
