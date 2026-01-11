using ESP32MockClient.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace ESP32MockClient.Services;

public class CryptoService
{
    private byte[]? _aesKey;

    public static string ComputeHmac(string message)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(MockClientConfiguration.SharedSecretCamKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash);
    }

    public static bool ValidateHmacResponse(string challenge, string serverResponse)
    {
        var expectedHex = ComputeHmac(challenge);

        if (expectedHex.Length != serverResponse.Length || expectedHex.Length != 64)
            return false;

        var expectedBytes = Convert.FromHexString(expectedHex);
        var responseBytes = Convert.FromHexString(serverResponse);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, responseBytes);
    }

    public static string GenerateChallengeString()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes);
    }

    public void SetSessionToken(string sessionTokenBase64)
    {
        _aesKey = Convert.FromBase64String(sessionTokenBase64);

        if (_aesKey.Length != 32)
            throw new ArgumentException($"Session token must decode to 32 bytes, got {_aesKey.Length}");
    }

    public bool EncryptAesGcm(byte[] plaintext, byte[] iv, out byte[] ciphertext, out byte[] tag)
    {
        ciphertext = [];
        tag = [];

        if (_aesKey == null || iv.Length != 12)
            return false;

        try
        {
            ciphertext = new byte[plaintext.Length];
            tag = new byte[16];

            using var aes = new AesGcm(_aesKey, 16);
            aes.Encrypt(iv, plaintext, ciphertext, tag);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static byte[] GenerateIv() => RandomNumberGenerator.GetBytes(12);
}
