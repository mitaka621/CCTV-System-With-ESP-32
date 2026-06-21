using ESP32MockClient.Configuration;
using ESP32MockClient.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ESP32MockClient.Services;

public class PreprovisionClientService
{
    private const string _domainTag = "campr-provision-v1";

    private readonly HttpClient _httpClient;

    public PreprovisionClientService()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<bool> VerifyAsync(PreprovisionCredentials credentials)
    {
        try
        {
            var fingerprintHex = ComputeFingerprintHex(credentials.PrivateKey);
            var bindingHash = ComputeBindingHash(credentials.DeviceId, fingerprintHex, credentials.Nonce);
            var signatureBase64 = SignHash(credentials.PrivateKey, bindingHash);

            var url = $"https://{credentials.ServerIp}:{MockClientConfiguration.ServerHttpsPort}/api/Preprovision";

            var payload = JsonSerializer.Serialize(new
            {
                DeviceId = credentials.DeviceId,
                DeviceSignature = signatureBase64
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            Console.WriteLine($"[Preprovision] Server responded {(int)response.StatusCode} ({response.StatusCode})");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Preprovision] Verification error: {ex.Message}");
            return false;
        }
    }

    private static string ComputeFingerprintHex(string privateKeyBase64)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);

        var spki = ecdsa.ExportSubjectPublicKeyInfo();
        var fingerprint = SHA256.HashData(spki);

        return Convert.ToHexString(fingerprint).ToLowerInvariant();
    }

    private static byte[] ComputeBindingHash(Guid deviceId, string fingerprintHex, string nonceBase64)
    {
        var header = Encoding.UTF8.GetBytes($"{_domainTag}|{deviceId:N}|{fingerprintHex}|");
        var nonce = Convert.FromBase64String(nonceBase64);

        var buffer = new byte[header.Length + nonce.Length];
        Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
        Buffer.BlockCopy(nonce, 0, buffer, header.Length, nonce.Length);

        return SHA256.HashData(buffer);
    }

    private static string SignHash(string privateKeyBase64, byte[] hash)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);

        var signature = ecdsa.SignHash(hash, DSASignatureFormat.Rfc3279DerSequence);
        return Convert.ToBase64String(signature);
    }
}
