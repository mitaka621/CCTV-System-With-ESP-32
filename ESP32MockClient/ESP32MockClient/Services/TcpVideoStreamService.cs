using ESP32MockClient.Configuration;
using System.Net.Sockets;

namespace ESP32MockClient.Services;

public class TcpVideoStreamService
{
    private readonly CryptoService _cryptoService;
    private readonly VideoFrameLoader _frameLoader;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;

    private int _failedConnectionAttempts;
    private int _failedFrameSends;

    public bool IsConnected => _tcpClient?.Connected ?? false;
    public int FailedConnectionAttempts => _failedConnectionAttempts;
    public int FailedFrameSends => _failedFrameSends;

    public TcpVideoStreamService(CryptoService cryptoService, VideoFrameLoader frameLoader)
    {
        _cryptoService = cryptoService;
        _frameLoader = frameLoader;
    }

    public bool Connect()
    {
        if (string.IsNullOrEmpty(MockClientConfiguration.ServerAddress))
            return false;

        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(MockClientConfiguration.ServerAddress, MockClientConfiguration.ServerTcpPort);
            _stream = _tcpClient.GetStream();
            _failedConnectionAttempts = 0;
            Console.WriteLine("[TCP] Connected");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TCP] Connection failed: {ex.Message}");
            _failedConnectionAttempts++;
            return false;
        }
    }

    public void Disconnect()
    {
        _stream?.Close();
        _tcpClient?.Close();
        _tcpClient = null;
        _stream = null;
    }

    public bool SendFrame(byte[] frameData)
    {
        if (_stream == null || !IsConnected)
            return false;

        try
        {
            var macBytes = ParseMacAddress(MockClientConfiguration.MacAddress);
            var iv = CryptoService.GenerateIv();

            if (!_cryptoService.EncryptAesGcm(frameData, iv, out var ciphertext, out var tag))
            {
                _failedFrameSends++;
                return false;
            }

            // Frame format: [4-byte length][6-byte MAC][12-byte IV][16-byte tag][ciphertext]
            var totalLen = 6 + iv.Length + tag.Length + ciphertext.Length;
            var sizeBuf = new byte[]
            {
                (byte)(totalLen >> 24),
                (byte)(totalLen >> 16),
                (byte)(totalLen >> 8),
                (byte)totalLen
            };

            _stream.Write(sizeBuf, 0, 4);
            _stream.Write(macBytes, 0, 6);
            _stream.Write(iv, 0, iv.Length);
            _stream.Write(tag, 0, tag.Length);
            _stream.Write(ciphertext, 0, ciphertext.Length);
            _stream.Flush();

            _failedFrameSends = 0;
            return true;
        }
        catch
        {
            _failedFrameSends++;
            Disconnect();
            return false;
        }
    }

    public async Task RunStreamingLoopAsync(CancellationToken ct)
    {
        Console.WriteLine("[TCP] Starting stream");

        var frameCount = 0;
        var startTime = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            if (!IsConnected)
            {
                if (!Connect())
                {
                    if (_failedConnectionAttempts >= MockClientConfiguration.MaxFailedConnectionAttempts)
                        break;

                    await Task.Delay(20000, ct);
                    continue;
                }
            }

            var frameData = _frameLoader.GetNextFrame();
            if (frameData == null)
            {
                await Task.Delay(100, ct);
                continue;
            }

            if (!SendFrame(frameData))
            {
                if (_failedFrameSends >= MockClientConfiguration.MaxFailedFrameSends)
                    break;
            }
            else
            {
                frameCount++;
                if (frameCount % 100 == 0)
                {
                    var fps = frameCount / (DateTime.UtcNow - startTime).TotalSeconds;
                    Console.WriteLine($"[TCP] {frameCount} frames ({fps:F1} fps)");
                }
            }

            _frameLoader.ReturnFrame();
            await Task.Delay(MockClientConfiguration.FrameDelayMs, ct);
        }

        Disconnect();
    }

    private static byte[] ParseMacAddress(string mac)
    {
        var parts = mac.Split(':');
        if (parts.Length != 6)
            throw new ArgumentException($"Invalid MAC: {mac}");
        return parts.Select(p => Convert.ToByte(p, 16)).ToArray();
    }
}
