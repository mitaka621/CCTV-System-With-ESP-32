using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ESP32MockClient.Models;

public class PreprovisionCredentials
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string WifiSSID { get; set; } = null!;

    public string WifiPassword { get; set; } = null!;

    public string PrivateKey { get; set; } = null!;

    public string ServerIp { get; set; } = null!;

    public string ServerIdentityPublicKey { get; set; } = null!;

    public Guid DeviceId { get; set; }

    public string Nonce { get; set; } = null!;

    public static bool TryParseFromUrl(string? input, out PreprovisionCredentials? credentials)
    {
        credentials = null;

        var token = ExtractPayloadToken(input);
        if (token == null)
        {
            return false;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(token));
            credentials = JsonSerializer.Deserialize<PreprovisionCredentials>(json, _jsonOptions);
        }
        catch
        {
            return false;
        }

        return credentials != null && credentials.IsComplete();
    }

    private bool IsComplete()
    {
        return DeviceId != Guid.Empty &&
               !string.IsNullOrEmpty(PrivateKey) &&
               !string.IsNullOrEmpty(ServerIp) &&
               !string.IsNullOrEmpty(ServerIdentityPublicKey) &&
               !string.IsNullOrEmpty(Nonce);
    }

    private static string? ExtractPayloadToken(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        input = input.Trim();

        var match = Regex.Match(input, @"[?&]d=([^&\s]+)");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        if (Regex.IsMatch(input, @"^[A-Za-z0-9_\-]+$"))
        {
            return input;
        }

        return null;
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');

        switch (normalized.Length % 4)
        {
            case 2:
                normalized += "==";
                break;
            case 3:
                normalized += "=";
                break;
        }

        return Convert.FromBase64String(normalized);
    }
}
