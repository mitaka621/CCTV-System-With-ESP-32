using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace CamPortal.Core.BackgroundServices
{
    public class FramesReceiverTcpService : BackgroundService
    {
        private readonly ILogger<FramesReceiverTcpService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICameraFramesManagerService _cameraFramesManagerService;
        private readonly IActiveCameraConnections _activeCameraConnections;
        private readonly IDeviceAuthenticatorService _deviceAuthenticatorService;

        private readonly int _port;

        public FramesReceiverTcpService(
            ILogger<FramesReceiverTcpService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ICameraFramesManagerService cameraFramesManagerService,
            IActiveCameraConnections activeCameraConnections,
            IDeviceAuthenticatorService deviceAuthenticatorService)

        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _cameraFramesManagerService = cameraFramesManagerService;
            _deviceAuthenticatorService = deviceAuthenticatorService;
            _activeCameraConnections = activeCameraConnections;

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

                    _ = Task.Run(async () =>
                    {
                        try { await HandleClientFramesAsync(client, stoppingToken); }
                        catch (Exception ex) { _logger.LogError(ex, "Unhandled exception in client handler"); }
                    }, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting client");
                }
            }

            listener.Stop();
        }

        private async Task HandleClientFramesAsync(TcpClient client, CancellationToken ct)
        {
            var remote = client.Client.RemoteEndPoint;
            _logger.LogInformation($"Client connected: {remote}");

            var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            var remoteIp = remoteEndPoint?.Address.MapToIPv4().ToString();

            using (var cameraServiceScope = _serviceProvider.CreateScope())
            {
                var cameraService = cameraServiceScope.ServiceProvider.GetRequiredService<ICameraService>();

                if (!await cameraService.DoesCameraExistWithStatusAsync(remoteIp ?? string.Empty, PairStatus.Paired))
                {
                    _logger.LogError($"Unauthorized connection: {remote}");

                    client.Close();

                    return;
                }
            }

            Guid cameraId = default;
            byte[]? key = default;
            DateTime expiresOn = default;
            bool sessionInitialized = false;
            try
            {
                using var stream = client.GetStream();

                while (!ct.IsCancellationRequested)
                {
                    byte[] sizeBuf = new byte[4];
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

                    int bytesRead = await stream.ReadAsync(sizeBuf.AsMemory(0, 4), timeoutCts.Token);

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
                        int read = await stream.ReadAsync(frameData, totalRead, totalLen - totalRead, timeoutCts.Token);
                        if (read == 0) break;
                        totalRead += read;
                    }

                    if (totalRead < totalLen)
                    {
                        _logger.LogWarning("Incomplete frame received");
                        break;
                    }

                    var mac = BitConverter.ToString(frameData.AsSpan(0, 6).ToArray()).Replace("-", ":");

                    using var frameScope = _serviceProvider.CreateScope();
                    var cameraService = frameScope.ServiceProvider.GetRequiredService<ICameraService>();

                    if (!sessionInitialized)
                    {
                        (key, expiresOn) = await cameraService.GetSessionTokenAsByteArrayAsync(remoteIp ?? string.Empty, mac ?? string.Empty);

                        cameraId = await cameraService.GetCameraIdAsync(remoteIp ?? string.Empty, mac ?? string.Empty);

                        ct = _activeCameraConnections.Register(cameraId, ct);

                        sessionInitialized = true;
                    }

                    if (key == null || key.Length != 32)
                    {
                        _logger.LogWarning($"Invalid session key for {remote}.");

                        break;
                    }

                    if (expiresOn < DateTime.Now)
                    {
                        _logger.LogWarning($"Session expired for {remote}.");

                        await cameraService.UpdateCameraStatusAsync(mac ?? string.Empty, PairStatus.SessionTokenExpired);

                        break;
                    }

                    var iv = frameData.AsSpan(6, 12);
                    var tag = frameData.AsSpan(18, 16);
                    var ciphertext = frameData.AsSpan(34);

                    var frameBuffer = _deviceAuthenticatorService.DecryptAesGcm(ciphertext, key, iv, tag);

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
                if (cameraId != default)
                {
                    _activeCameraConnections.Unregister(cameraId);
                    _cameraFramesManagerService.CloseProcessedFramesCameraChannel(cameraId);
                }

                client.Close();
                _logger.LogInformation($"Client {remote} disconnected");
            }
        }
    }
}
