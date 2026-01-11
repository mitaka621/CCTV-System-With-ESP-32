using ESP32MockClient.Configuration;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ESP32MockClient.Services;

public class ChallengeHttpServer : IDisposable
{
    private readonly AuthenticationService _authService;
    private readonly HttpListener _listener;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _disposed;

    public bool IsPaired { get; private set; }
    public event EventHandler<bool>? PairingCompleted;

    public ChallengeHttpServer(AuthenticationService authService)
    {
        _authService = authService;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{MockClientConfiguration.LocalHttpPort}/");
    }

    public void Start()
    {
        if (_disposed) return;

        _cts = new CancellationTokenSource();

        try
        {
            _listener.Start();
            _isRunning = true;
            Console.WriteLine($"[HttpServer] Listening on port {MockClientConfiguration.LocalHttpPort}");
            _ = Task.Run(() => ListenLoop(_cts.Token));
        }
        catch (HttpListenerException ex)
        {
            Console.WriteLine($"[HttpServer] Failed: {ex.Message}");
            Console.WriteLine("[HttpServer] Run as Administrator or: netsh http add urlacl url=http://+:77/ user=Everyone");
            throw;
        }
    }

    public void Stop()
    {
        if (_disposed || !_isRunning) return;

        _cts?.Cancel();
        try
        {
            _listener.Stop();
        }
        catch { }
        _isRunning = false;
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isRunning)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context), ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested || !_isRunning)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch { }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        if (request.Url?.AbsolutePath == "/challenge" && request.HttpMethod == "GET")
        {
            await HandleChallengeRequest(context);
        }
        else
        {
            response.StatusCode = 404;
            response.Close();
        }
    }

    private async Task HandleChallengeRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        var challenge = request.QueryString["challenge"];
        if (string.IsNullOrEmpty(challenge))
        {
            response.StatusCode = 400;
            var errorBytes = Encoding.UTF8.GetBytes("Missing 'challenge' parameter");
            await response.OutputStream.WriteAsync(errorBytes);
            response.Close();
            return;
        }

        var serverAddress = request.RemoteEndPoint?.Address.ToString();
        if (serverAddress == null)
        {
            response.StatusCode = 500;
            response.Close();
            return;
        }

        if (serverAddress.StartsWith("::ffff:"))
            serverAddress = serverAddress[7..];

        MockClientConfiguration.ServerAddress = serverAddress;
        MockClientConfiguration.BaseServerUrl = $"https://{serverAddress}:{MockClientConfiguration.ServerHttpsPort}";

        Console.WriteLine($"[HttpServer] Challenge from {serverAddress}");

        var hmac = CryptoService.ComputeHmac(challenge);
        var responseObj = new { Hmac = hmac, MacAddress = MockClientConfiguration.MacAddress };
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(responseObj));

        response.ContentType = "application/json";
        response.StatusCode = 200;
        await response.OutputStream.WriteAsync(jsonBytes);
        response.Close();

        IsPaired = await _authService.GetSessionTokenFromServerAsync();
        PairingCompleted?.Invoke(this, IsPaired);

        Console.WriteLine($"[HttpServer] Pairing: {(IsPaired ? "success" : "failed")}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _cts?.Dispose();
    }
}
