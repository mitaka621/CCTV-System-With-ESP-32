using System.Security.Cryptography;
using System.Text;
using BlazorCamPortal.Contracts.Services;
using Microsoft.Extensions.Configuration;

namespace BlazorCamPortal.Services
{
    public class DeviceAuthenticator : IDeviceAuthenticator
    {
        private readonly IConfiguration _configuration;
        private readonly string _sharedSecretKey;

        public DeviceAuthenticator(IConfiguration configuration)
        {
            _configuration = configuration;

            _sharedSecretKey = _configuration["DeviceAuthenticator:SharedSecretKey"] ?? throw new InvalidOperationException("Shared secret key is not configured.");
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

        private string ComputeHmac(string challenge)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_sharedSecretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(challenge));
            return Convert.ToHexString(hash);
        }
    }
}
