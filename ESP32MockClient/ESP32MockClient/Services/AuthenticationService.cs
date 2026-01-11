using ESP32MockClient.Configuration;
using System.Text.Json;

namespace ESP32MockClient.Services;

public class AuthenticationService
{
    private readonly CryptoService _cryptoService;
    private readonly HttpClient _httpClient;

    public AuthenticationService(CryptoService cryptoService)
    {
        _cryptoService = cryptoService;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<bool> IsServerValidAsync()
    {
        var baseServerUrl = MockClientConfiguration.BaseServerUrl;
        if (string.IsNullOrEmpty(baseServerUrl))
            return false;

        var challenge = CryptoService.GenerateChallengeString();
        var url = $"{baseServerUrl}/api/deviceauthenticator/challenge?espCameraChallenge={challenge}&mac={MockClientConfiguration.MacAddress}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return false;

            var jsonContent = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonContent);
            var serverHmac = doc.RootElement.GetProperty("hmac").GetString();

            if (string.IsNullOrEmpty(serverHmac))
                return false;

            var isValid = CryptoService.ValidateHmacResponse(challenge, serverHmac);
            Console.WriteLine($"[Auth] Server validation: {(isValid ? "passed" : "failed")}");
            return isValid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Auth] Validation error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> GetSessionTokenFromServerAsync()
    {
        if (!await IsServerValidAsync())
            return false;

        var baseServerUrl = MockClientConfiguration.BaseServerUrl;
        if (string.IsNullOrEmpty(baseServerUrl))
            return false;

        var url = $"{baseServerUrl}/api/deviceauthenticator/serverSession?mac={MockClientConfiguration.MacAddress}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return false;

            var jsonContent = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonContent);
            var sessionToken = doc.RootElement.GetProperty("sessionToken").GetString();

            if (string.IsNullOrEmpty(sessionToken))
                return false;

            MockClientConfiguration.SessionToken = sessionToken;
            _cryptoService.SetSessionToken(sessionToken);
            Console.WriteLine("[Auth] Session token received");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Auth] Session token error: {ex.Message}");
            return false;
        }
    }
}
