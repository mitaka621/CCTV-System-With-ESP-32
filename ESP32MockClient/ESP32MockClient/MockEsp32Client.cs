using ESP32MockClient.Models;
using ESP32MockClient.Services;

namespace ESP32MockClient;

public class MockEsp32Client : IDisposable
{
    private readonly VideoFrameLoader _frameLoader;
    private readonly PreprovisionClientService _preprovisionService;
    private readonly SecureSessionService _secureSessionService;

    private PreprovisionCredentials? _credentials;
    private CancellationTokenSource? _cts;

    public MockEsp32Client()
    {
        _frameLoader = new VideoFrameLoader();
        _preprovisionService = new PreprovisionClientService();
        _secureSessionService = new SecureSessionService(_frameLoader);
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (!await SetupAsync(_cts.Token))
        {
            Console.WriteLine("[Main] Setup failed");
            return;
        }

        await _secureSessionService.RunStreamingLoopAsync(_credentials!, _cts.Token);
    }

    private async Task<bool> SetupAsync(CancellationToken ct)
    {
        Console.WriteLine("[Setup] Initializing...");
        _frameLoader.Initialize();

        while (!ct.IsCancellationRequested)
        {
            var credentials = PromptForCredentials();
            if (credentials == null)
            {
                Console.WriteLine("[Setup] That URL did not contain valid preprovision credentials. Try again.");
                continue;
            }

            Console.WriteLine($"[Setup] Extracted credentials for device {credentials.DeviceId}. Verifying with {credentials.ServerIp}...");

            if (await _preprovisionService.VerifyAsync(credentials))
            {
                _credentials = credentials;
                Console.WriteLine("[Setup] Preprovision verification accepted by server");
                return true;
            }

            Console.WriteLine("[Setup] Preprovision verification rejected. Start a fresh wizard attempt and paste the new QR URL to retry.");
        }

        return false;
    }

    private static PreprovisionCredentials? PromptForCredentials()
    {
        Console.WriteLine();
        Console.WriteLine("Reach step 4 of the pairing wizard, then paste the preprovision QR code URL here:");
        Console.Write("> ");

        var input = Console.ReadLine();
        return PreprovisionCredentials.TryParseFromUrl(input, out var credentials) ? credentials : null;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _secureSessionService.Dispose();
        _frameLoader.Dispose();
    }
}
