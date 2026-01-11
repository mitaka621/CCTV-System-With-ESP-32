using ESP32MockClient.Configuration;
using ESP32MockClient.Services;

namespace ESP32MockClient;

public class MockEsp32Client : IDisposable
{
    private readonly CryptoService _cryptoService;
    private readonly AuthenticationService _authService;
    private readonly VideoFrameLoader _frameLoader;
    private readonly ChallengeHttpServer _httpServer;
    private readonly TcpVideoStreamService _tcpService;

    private bool _isPaired;
    private CancellationTokenSource? _cts;

    public MockEsp32Client()
    {
        _cryptoService = new CryptoService();
        _authService = new AuthenticationService(_cryptoService);
        _frameLoader = new VideoFrameLoader();
        _httpServer = new ChallengeHttpServer(_authService);
        _tcpService = new TcpVideoStreamService(_cryptoService, _frameLoader);
        _httpServer.PairingCompleted += (_, success) => _isPaired = success;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (!Setup())
        {
            Console.WriteLine("[Main] Setup failed");
            return;
        }

        await MainLoopAsync(_cts.Token);
    }

    private bool Setup()
    {
        Console.WriteLine("[Setup] Initializing...");
        _frameLoader.Initialize();

        _isPaired = !string.IsNullOrEmpty(MockClientConfiguration.SessionToken);

        if (_isPaired)
        {
            try
            {
                _cryptoService.SetSessionToken(MockClientConfiguration.SessionToken!);
            }
            catch
            {
                _isPaired = false;
                MockClientConfiguration.SessionToken = null;
            }
        }

        if (!_isPaired)
        {
            try
            {
                _httpServer.Start();
                Console.WriteLine("[Setup] Waiting for server handshake on port 77...");

                while (!_isPaired && !_cts!.Token.IsCancellationRequested)
                    Thread.Sleep(100);
            }
            catch
            {
                return false;
            }
        }

        if (!_isPaired)
            return false;

        _httpServer.Stop();
        Console.WriteLine("[Setup] Complete");
        return true;
    }

    private async Task MainLoopAsync(CancellationToken ct)
    {
        var failedPairingAttempts = 0;

        while (!ct.IsCancellationRequested)
        {
            if (!_isPaired)
            {
                if (string.IsNullOrEmpty(MockClientConfiguration.BaseServerUrl))
                    break;

                _isPaired = await _authService.GetSessionTokenFromServerAsync();

                if (!_isPaired)
                {
                    failedPairingAttempts++;
                    if (failedPairingAttempts >= MockClientConfiguration.MaxFailedPairingAttempts)
                    {
                        ForgetServer();
                        break;
                    }
                    await Task.Delay(5000, ct);
                    continue;
                }
                failedPairingAttempts = 0;
            }

            await _tcpService.RunStreamingLoopAsync(ct);

            if (_tcpService.FailedFrameSends >= MockClientConfiguration.MaxFailedFrameSends)
                _isPaired = false;

            if (_tcpService.FailedConnectionAttempts >= MockClientConfiguration.MaxFailedConnectionAttempts)
            {
                ForgetServer();
                break;
            }
        }
    }

    private static void ForgetServer()
    {
        MockClientConfiguration.SessionToken = null;
        MockClientConfiguration.ServerAddress = null;
        MockClientConfiguration.BaseServerUrl = null;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _httpServer.Dispose();
        _frameLoader.Dispose();
    }
}
