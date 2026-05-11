using CamPortal.Contracts.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace CamPortal.Core.Services
{
    public class DeviceAuthenticatorService : IDeviceAuthenticatorService
    {
        private readonly IConfiguration _configuration;
        private readonly string _sharedSecretKey;

        public DeviceAuthenticatorService(IConfiguration configuration)
        {
            _configuration = configuration;

            _sharedSecretKey = _configuration.GetSection("DeviceAuthenticator")["SharedSecretCamKey"] ?? throw new InvalidOperationException("Shared secret key is not configured.");
        }

        public string GenerateChallenge()
        {
            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[16];
            rng.GetBytes(bytes);
            return Convert.ToHexString(bytes);
        }

        public bool ValidateDeviceResponse(string challenge, string deviceResponse)
        {
            var expectedHex = ComputeHmac(challenge);

            var expectedBytes = Convert.FromHexString(expectedHex);
            var responseBytes = Convert.FromHexString(deviceResponse);

            return CryptographicOperations.FixedTimeEquals(expectedBytes, responseBytes);
        }

        public string ComputeHmac(string challenge)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_sharedSecretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(challenge));
            return Convert.ToHexString(hash);
        }

        public string GenerateSessionToken(int keySizeInBits = 256)
        {
            var rng = RandomNumberGenerator.Create();

            var key = new byte[keySizeInBits / 8];
            rng.GetBytes(key);

            return Convert.ToBase64String(key);
        }

        public byte[] DecryptAesGcm(Span<byte> ciphertext, Span<byte> key, Span<byte> iv, Span<byte> tag)
        {
            byte[] plaintext = new byte[ciphertext.Length];

            using (var aes = new AesGcm(key, tag.Length))
            {
                aes.Decrypt(iv, ciphertext, tag, plaintext);
            }
            return plaintext;
        }
    }
}
