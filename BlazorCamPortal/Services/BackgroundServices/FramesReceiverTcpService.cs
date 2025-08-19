using System.Net;
using System.Net.Sockets;
using BlazorCamPortal.Contracts.Abstractions.Services;
using BlazorCamPortal.Contracts.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorCamPortal.Core.BackgroundServices
{
    public class FramesReceiverTcpService : BackgroundService
    {
        private readonly ILogger<FramesReceiverTcpService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICameraFramesManagerService _cameraFramesManagerService;

        private readonly int _port;

        public FramesReceiverTcpService(
            ILogger<FramesReceiverTcpService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ICameraFramesManagerService cameraFramesManagerService)

        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _cameraFramesManagerService = cameraFramesManagerService;

            _port = int.Parse(configuration.GetSection("TCPServerConfig")["Port"] ?? throw new ArgumentNullException("TCP server port not configured"));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();
            _logger.LogInformation($"TCP server started on port {_port}");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync(stoppingToken);

                    _ = Task.Run(() => HandleClientFramesAsync(client, stoppingToken), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting client");
                }
            }

            listener.Stop();
        }

        private async Task HandleClientFramesAsync(TcpClient client, CancellationToken ct)
        {
            var cameraService = _serviceProvider.CreateScope()
                .ServiceProvider
                .GetRequiredService<ICameraService>();

            var deviceAuthenticatorService = _serviceProvider.CreateScope()
                .ServiceProvider
                .GetRequiredService<IDeviceAuthenticatorService>();

            var remote = client.Client.RemoteEndPoint;
            _logger.LogInformation($"Client connected: {remote}");

            var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            var remoteIp = remoteEndPoint?.Address.MapToIPv4().ToString();

            if (!await cameraService.DoesCameraExistWithStatusAsync(remoteIp ?? string.Empty, PairStatus.Paired))
            {
                _logger.LogError($"Unauthorized connection: {remote}");

                client.Close();

                return;
            }

            try
            {
                using var stream = client.GetStream();

                while (!ct.IsCancellationRequested && client.Connected)
                {
                    byte[] sizeBuf = new byte[4];
                    int bytesRead = await stream.ReadAsync(sizeBuf.AsMemory(0, 4), ct);
                    if (bytesRead < 4)
                    {
                        _logger.LogWarning("Incomplete size received");
                        break;
                    }

                    int totalLen = (sizeBuf[0] << 24) | (sizeBuf[1] << 16) | (sizeBuf[2] << 8) | sizeBuf[3];
                    if (totalLen <= 0 || totalLen > 10_000_000)
                    {
                        _logger.LogWarning($"Invalid frame length: {totalLen}");
                        break;
                    }

                    byte[] frameData = new byte[totalLen];
                    int totalRead = 0;
                    while (totalRead < totalLen)
                    {
                        int read = await stream.ReadAsync(frameData, totalRead, totalLen - totalRead, ct);
                        if (read == 0) break;
                        totalRead += read;
                    }

                    if (totalRead < totalLen)
                    {
                        _logger.LogWarning("Incomplete frame received");
                        break;
                    }

                    var mac = BitConverter.ToString(frameData.AsSpan(0, 6).ToArray()).Replace("-", ":");
                    var iv = frameData.AsSpan(6, 12).ToArray();
                    var tag = frameData.AsSpan(18, 16).ToArray();
                    var ciphertext = frameData.AsSpan(34).ToArray();

                    var (key, isExpired) = await cameraService.GetSessionTokenAsByteArrayAsync(remoteIp ?? string.Empty, mac ?? string.Empty);

                    if (key == null || key.Length != 32)
                    {
                        _logger.LogWarning($"Invalid session key for {remote}.");

                        break;
                    }

                    if (isExpired)
                    {
                        _logger.LogWarning($"Session expired for {remote}.");

                        await cameraService.UpdateCameraStatusAsync(mac ?? string.Empty, PairStatus.SessionTokenExpired);
                    }

                    var frameBuffer = deviceAuthenticatorService.DecryptAesGcm(ciphertext, key, iv, tag);

                    var cameraId = await cameraService.GetCameraIdAsync(remoteIp ?? string.Empty, mac ?? string.Empty);

                    _cameraFramesManagerService.AddFrame(cameraId, frameBuffer);
                }

                _logger.LogWarning($"Client disconnected {remote}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling client {remote}");
            }
            finally
            {
                client.Close();
                _logger.LogInformation($"Client {remote} disconnected");
            }
        }
    }
}
