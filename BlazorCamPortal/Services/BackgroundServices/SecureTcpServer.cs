using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace CamPortal.Core.BackgroundServices
{
    public class SecureTcpServer : BackgroundService
    {
        private readonly ILogger<SecureTcpServer> _logger;
        private readonly ISecureHandshake _secureHandshake;
        private readonly IReadOnlyDictionary<DeviceTypeCategories, IDeviceSessionHandler> _deviceSessionHandlers;
        private readonly int _port;

        public SecureTcpServer(
            ILogger<SecureTcpServer> logger,
            IConfiguration configuration,
            ISecureHandshake secureHandshake,
            IEnumerable<IDeviceSessionHandler> deviceSessionHandlers)
        {
            _logger = logger;
            _secureHandshake = secureHandshake;
            _deviceSessionHandlers = deviceSessionHandlers.ToDictionary(handler => handler.DeviceCategory);

            _port = int.Parse(configuration.GetSection("TCPServerConfig")["Port"]
                ?? throw new ArgumentNullException("TCP server port not configured"));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();
            _logger.LogInformation("Secure stream TCP server started on port {Port}", _port);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync(stoppingToken);

                    _ = Task.Run(async () =>
                    {
                        try { await HandleClientAsync(client, stoppingToken); }
                        catch (Exception ex) { _logger.LogError(ex, "Unhandled exception in device session"); }
                    }, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting stream client");
                }
            }

            listener.Stop();
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            var remoteEndpoint = client.Client.RemoteEndPoint;
            _logger.LogInformation("Stream client connected: {RemoteEndpoint}", remoteEndpoint);

            try
            {
                using var stream = client.GetStream();

                var device = await _secureHandshake.AuthorizeAsync(stream, remoteEndpoint, cancellationToken);
                if (device == null)
                {
                    return;
                }

                if (!_deviceSessionHandlers.TryGetValue(device.DeviceVariant, out var deviceSessionHandler))
                {
                    _logger.LogWarning(
                        "Stream rejected from {RemoteEndpoint}: no handler for device {DeviceId} variant {Variant}",
                        remoteEndpoint, device.Id, device.DeviceVariant);
                    return;
                }

                await using var secureChannel = await _secureHandshake.EstablishAsync(stream, device, cancellationToken);
                if (secureChannel == null)
                {
                    return;
                }

                using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                var receiveLoop = deviceSessionHandler.RunRecieveLoopAsync(secureChannel, device, sessionCts.Token);
                var sendLoop = deviceSessionHandler.RunSendLoopAsync(secureChannel, device, sessionCts.Token);

                try
                {
                    await Task.WhenAny(receiveLoop, sendLoop);
                }
                finally
                {
                    await sessionCts.CancelAsync();
                    await Task.WhenAll(
                        receiveLoop,
                        sendLoop);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stream session error for {RemoteEndpoint}", remoteEndpoint);
            }
            finally
            {
                client.Close();
                _logger.LogInformation("Stream client {RemoteEndpoint} disconnected", remoteEndpoint);
            }
        }
    }
}
