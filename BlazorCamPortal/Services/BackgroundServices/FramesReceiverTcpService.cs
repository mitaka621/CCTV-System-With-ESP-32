using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.CameraConfigurationDtos;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.SecureStreamingDtos;
using CamPortal.Contracts.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace CamPortal.Core.BackgroundServices
{
    public class FramesReceiverTcpService : BackgroundService
    {
        private const string _domainTag = "CAMPR-STREAM-V1";
        private const string _hkdfInfo = "CAMPR-STREAM-V1-derived";
        private const int _deviceIdLen = 16;
        private const int _nonceLen = 32;
        private const int _ephemeralPubLen = 65;
        private const int _signatureLen = 64;
        private const int _ivBaseLen = 4;
        private const int _sessionIdLen = 16;
        private const int _gcmTagLen = 16;
        private const int _gcmIvLen = 12;
        private const int _seqLen = 8;
        private const int _frameHeaderLen = 4 + _seqLen + _gcmTagLen;
        private const int _resolutionHeaderLen = 8;

        private readonly ILogger<FramesReceiverTcpService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICameraFramesManagerService _cameraFramesManagerService;
        private readonly IActiveCameraConnections _activeCameraConnections;
        private readonly IServerIdentityService _serverIdentityService;
        private readonly ICameraConfigurationService _cameraConfigurationService;

        private readonly int _port;
        private readonly SecureStreamSettingsDto _streamSettings;

        public FramesReceiverTcpService(
            ILogger<FramesReceiverTcpService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ICameraFramesManagerService cameraFramesManagerService,
            IActiveCameraConnections activeCameraConnections,
            IServerIdentityService serverIdentityService,
            ICameraConfigurationService cameraConfigurationService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _cameraFramesManagerService = cameraFramesManagerService;
            _activeCameraConnections = activeCameraConnections;
            _serverIdentityService = serverIdentityService;
            _cameraConfigurationService = cameraConfigurationService;

            _port = int.Parse(configuration.GetSection("TCPServerConfig")["Port"]
                ?? throw new ArgumentNullException("TCP server port not configured"));

            var streamingSection = configuration.GetSection("SecureStreaming");
            _streamSettings = new SecureStreamSettingsDto
            {
                HandshakeTimeoutSeconds = ParseIntOrDefault(streamingSection["HandshakeTimeoutSeconds"], 10),
                FrameReadTimeoutSeconds = ParseIntOrDefault(streamingSection["FrameReadTimeoutSeconds"], 15),
                MaxFrameBytes = ParseIntOrDefault(streamingSection["MaxFrameBytes"], 4_000_000),
                ReplayWindow = ParseIntOrDefault(streamingSection["ReplayWindow"], 2),
                MaxSessionDurationMinutes = ParseIntOrDefault(streamingSection["MaxSessionDurationMinutes"], 120),
                MaxSessionFrames = ParseLongOrDefault(streamingSection["MaxSessionFrames"], 4_000_000_000L),
            };
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
                        catch (Exception ex) { _logger.LogError(ex, "Unhandled exception in stream session"); }
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

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            var remote = client.Client.RemoteEndPoint;
            _logger.LogInformation("Stream client connected: {RemoteEndpoint}", remote);

            Guid cameraId = default;

            try
            {
                using var stream = client.GetStream();

                var device = await ReadDeviceHelloAndAuthorizeAsync(stream, remote, ct);
                if (device == null)
                {
                    return;
                }

                cameraId = device.Id;
                var sessionCt = _activeCameraConnections.Register(cameraId, ct);

                var sessionMaterial = await PerformHandshakeAsync(stream, device, sessionCt);
                if (sessionMaterial == null)
                {
                    return;
                }

                await RunFrameLoopAsync(stream, device, sessionMaterial, sessionCt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stream session error for {RemoteEndpoint}", remote);
            }
            finally
            {
                if (cameraId != default)
                {
                    _activeCameraConnections.TryDisconnect(cameraId);
                    _cameraFramesManagerService.CloseProcessedFramesCameraChannel(cameraId);
                }

                client.Close();
                _logger.LogInformation("Stream client {RemoteEndpoint} disconnected", remote);
            }
        }

        private async Task<DeviceStreamingHandshakeDto?> ReadDeviceHelloAndAuthorizeAsync(
            NetworkStream stream,
            EndPoint? remote,
            CancellationToken ct)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_streamSettings.HandshakeTimeoutSeconds));

            var deviceIdBytes = new byte[_deviceIdLen];
            await ReadExactAsync(stream, deviceIdBytes, timeoutCts.Token);

            var deviceId = new Guid(deviceIdBytes, bigEndian: true);

            DeviceStreamingHandshakeDto? device;
            using (var scope = _serviceProvider.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                device = await repo.GetDeviceForStreamingHandshakeAsync(deviceId);
            }

            if (device == null)
            {
                _logger.LogWarning("Stream rejected from {RemoteEndpoint}: device {DeviceId} not found", remote, deviceId);
                return null;
            }

            if (device.PairStatus != DevicePairStatus.Paired)
            {
                _logger.LogWarning(
                    "Stream rejected from {RemoteEndpoint}: device {DeviceId} not paired (status={Status})",
                    remote, deviceId, device.PairStatus);
                return null;
            }

            if (device.DeviceVariant != DeviceTypeCategories.Camera)
            {
                _logger.LogWarning(
                    "Stream rejected from {RemoteEndpoint}: device {DeviceId} is not a camera (variant={Variant})",
                    remote, deviceId, device.DeviceVariant);
                return null;
            }

            return device;
        }

        private async Task<Session?> PerformHandshakeAsync(
            NetworkStream stream,
            DeviceStreamingHandshakeDto device,
            CancellationToken ct)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_streamSettings.HandshakeTimeoutSeconds));

            var deviceIdBytes = new byte[_deviceIdLen];
            device.Id.TryWriteBytes(deviceIdBytes, bigEndian: true, out _);

            var nonceServer = RandomNumberGenerator.GetBytes(_nonceLen);

            using var ephemeralServer = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var ephemeralServerParams = ephemeralServer.ExportParameters(false);
            var ephemeralServerPubBytes = ExportUncompressedPoint(ephemeralServerParams.Q);

            var serverSignatureInput = Concat(
                Encoding.ASCII.GetBytes(_domainTag + "\0"),
                deviceIdBytes,
                nonceServer,
                ephemeralServerPubBytes);
            var serverSignatureHash = SHA256.HashData(serverSignatureInput);
            var serverSignature = _serverIdentityService.SignHashRawP1363(serverSignatureHash);

            var serverHello = new byte[_nonceLen + _ephemeralPubLen + _signatureLen];
            Buffer.BlockCopy(nonceServer, 0, serverHello, 0, _nonceLen);
            Buffer.BlockCopy(ephemeralServerPubBytes, 0, serverHello, _nonceLen, _ephemeralPubLen);
            Buffer.BlockCopy(serverSignature, 0, serverHello, _nonceLen + _ephemeralPubLen, _signatureLen);

            await stream.WriteAsync(serverHello, timeoutCts.Token);
            await stream.FlushAsync(timeoutCts.Token);

            var deviceHello = new byte[_nonceLen + _ephemeralPubLen + _signatureLen];
            await ReadExactAsync(stream, deviceHello, timeoutCts.Token);

            var nonceDevice = new byte[_nonceLen];
            var ephemeralDevicePubBytes = new byte[_ephemeralPubLen];
            var deviceSignature = new byte[_signatureLen];
            Buffer.BlockCopy(deviceHello, 0, nonceDevice, 0, _nonceLen);
            Buffer.BlockCopy(deviceHello, _nonceLen, ephemeralDevicePubBytes, 0, _ephemeralPubLen);
            Buffer.BlockCopy(deviceHello, _nonceLen + _ephemeralPubLen, deviceSignature, 0, _signatureLen);

            if (ephemeralDevicePubBytes[0] != 0x04)
            {
                _logger.LogWarning("Stream {DeviceId}: device ephemeral public key has invalid format byte", device.Id);
                return null;
            }

            var deviceSignatureInput = Concat(
                Encoding.ASCII.GetBytes(_domainTag + "\0"),
                deviceIdBytes,
                nonceServer,
                nonceDevice,
                ephemeralServerPubBytes,
                ephemeralDevicePubBytes);
            var deviceSignatureHash = SHA256.HashData(deviceSignatureInput);

            using var deviceIdentityKey = ECDsa.Create();
            try
            {
                deviceIdentityKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(device.PublicKey), out _);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stream {DeviceId}: failed to import device public key", device.Id);
                return null;
            }

            //checking if the deviceSignatureHash (produced server side) matches the deviceSignature (produced device side from the same parameters as the server side hash) and sent to the server
            if (!deviceIdentityKey.VerifyHash(deviceSignatureHash, deviceSignature, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                _logger.LogWarning("Stream {DeviceId}: device signature verification failed", device.Id);
                return null;
            }

            using var ephemeralDevicePubKey = ECDiffieHellman.Create();
            try
            {
                ephemeralDevicePubKey.ImportParameters(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint
                    {
                        X = ephemeralDevicePubBytes.AsSpan(1, 32).ToArray(),
                        Y = ephemeralDevicePubBytes.AsSpan(33, 32).ToArray(),
                    },
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stream {DeviceId}: invalid device ephemeral public key", device.Id);
                return null;
            }

            byte[] sharedSecret;
            try
            {
                sharedSecret = ephemeralServer.DeriveRawSecretAgreement(ephemeralDevicePubKey.PublicKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stream {DeviceId}: ECDH derive failed", device.Id);
                return null;
            }

            var hkdfSalt = new byte[_nonceLen * 2];
            Buffer.BlockCopy(nonceServer, 0, hkdfSalt, 0, _nonceLen);
            Buffer.BlockCopy(nonceDevice, 0, hkdfSalt, _nonceLen, _nonceLen);

            const int derivedLen = 32 + _ivBaseLen + _sessionIdLen;
            var derivedKeyMaterial = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm: sharedSecret,
                outputLength: derivedLen,
                salt: hkdfSalt,
                info: Encoding.ASCII.GetBytes(_hkdfInfo));

            CryptographicOperations.ZeroMemory(sharedSecret);

            var sessionKey = new byte[32];
            var ivBase = new byte[_ivBaseLen];
            var sessionId = new byte[_sessionIdLen];
            Buffer.BlockCopy(derivedKeyMaterial, 0, sessionKey, 0, 32);
            Buffer.BlockCopy(derivedKeyMaterial, 32, ivBase, 0, _ivBaseLen);
            Buffer.BlockCopy(derivedKeyMaterial, 32 + _ivBaseLen, sessionId, 0, _sessionIdLen);
            CryptographicOperations.ZeroMemory(derivedKeyMaterial);

            _logger.LogInformation(
                "Stream {DeviceId}: handshake complete, session {SessionId}",
                device.Id,
                Convert.ToHexString(sessionId));

            return new Session
            {
                SessionKey = sessionKey,
                IvBase = ivBase,
                SessionId = sessionId,
                DeviceIdBytes = deviceIdBytes,
                StartedAt = DateTime.UtcNow,
            };
        }

        private async Task RunFrameLoopAsync(
            NetworkStream stream,
            DeviceStreamingHandshakeDto device,
            Session session,
            CancellationToken ct)
        {
            using var aes = new AesGcm(session.SessionKey, _gcmTagLen);

            ulong lastSeq = 0;
            ulong totalFrames = 0;
            bool resolutionSaved = false;

            var headerBuf = new byte[_frameHeaderLen];
            var ivBuf = new byte[_gcmIvLen];
            Buffer.BlockCopy(session.IvBase, 0, ivBuf, 0, _ivBaseLen);

            var aadBuf = new byte[_sessionIdLen + _deviceIdLen + _seqLen];
            Buffer.BlockCopy(session.SessionId, 0, aadBuf, 0, _sessionIdLen);
            Buffer.BlockCopy(session.DeviceIdBytes, 0, aadBuf, _sessionIdLen, _deviceIdLen);

            var sessionExpiresAt = session.StartedAt.AddMinutes(_streamSettings.MaxSessionDurationMinutes);

            while (!ct.IsCancellationRequested)
            {
                if (DateTime.UtcNow >= sessionExpiresAt)
                {
                    _logger.LogInformation("Stream {DeviceId}: session expired by time", device.Id);
                    return;
                }

                if (totalFrames >= (ulong)_streamSettings.MaxSessionFrames)
                {
                    _logger.LogInformation("Stream {DeviceId}: session expired by frame count", device.Id);
                    return;
                }

                using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                idleCts.CancelAfter(TimeSpan.FromSeconds(_streamSettings.FrameReadTimeoutSeconds));

                try
                {
                    await ReadExactAsync(stream, headerBuf, idleCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    _logger.LogWarning("Stream {DeviceId}: idle timeout", device.Id);
                    return;
                }

                var totalLen = BinaryPrimitives.ReadUInt32BigEndian(headerBuf.AsSpan(0, 4));
                var seq = BinaryPrimitives.ReadUInt64BigEndian(headerBuf.AsSpan(4, 8));

                if (totalLen < _seqLen + _gcmTagLen + _resolutionHeaderLen + 1
                    || totalLen > _seqLen + _gcmTagLen + (uint)_streamSettings.MaxFrameBytes)
                {
                    _logger.LogWarning("Stream {DeviceId}: invalid frame total length {Len}", device.Id, totalLen);
                    return;
                }

                if (seq == 0)
                {
                    _logger.LogWarning("Stream {DeviceId}: seq 0 is reserved", device.Id);
                    return;
                }

                if (seq <= lastSeq)
                {
                    _logger.LogWarning("Stream {DeviceId}: replay/out-of-order seq {Seq} (last {Last})", device.Id, seq, lastSeq);
                    return;
                }

                if (seq - lastSeq > (ulong)_streamSettings.ReplayWindow)
                {
                    _logger.LogWarning("Stream {DeviceId}: seq gap too large {Seq} (last {Last})", device.Id, seq, lastSeq);
                    return;
                }

                var tag = new byte[_gcmTagLen];
                Buffer.BlockCopy(headerBuf, _frameHeaderLen - _gcmTagLen, tag, 0, _gcmTagLen);

                var ciphertextLen = (int)(totalLen - _seqLen - _gcmTagLen);
                var ciphertext = new byte[ciphertextLen];
                try
                {
                    await ReadExactAsync(stream, ciphertext, idleCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    _logger.LogWarning("Stream {DeviceId}: idle timeout reading frame body", device.Id);
                    return;
                }

                BinaryPrimitives.WriteUInt64BigEndian(ivBuf.AsSpan(_ivBaseLen, _seqLen), seq);
                BinaryPrimitives.WriteUInt64BigEndian(aadBuf.AsSpan(_sessionIdLen + _deviceIdLen, _seqLen), seq);

                var plaintext = new byte[ciphertextLen];
                try
                {
                    aes.Decrypt(ivBuf, ciphertext, tag, plaintext, aadBuf);
                }
                catch (CryptographicException ex)
                {
                    _logger.LogWarning(ex, "Stream {DeviceId}: GCM decrypt failed seq {Seq}", device.Id, seq);
                    return;
                }

                lastSeq = seq;
                totalFrames++;

                if (!resolutionSaved)
                {
                    resolutionSaved = true;

                    var resolutionWidth = (int)BinaryPrimitives.ReadUInt32BigEndian(plaintext.AsSpan(0, 4));
                    var resolutionHeight = (int)BinaryPrimitives.ReadUInt32BigEndian(plaintext.AsSpan(4, 4));


                    await _cameraConfigurationService.SetCameraResolutionAsync(new CameraResolutionDto()
                    {
                        CameraId = device.Id,
                        Width = resolutionWidth,
                        Height = resolutionHeight
                    });
                }

                var jpeg = plaintext.AsSpan(_resolutionHeaderLen).ToArray();

                _cameraFramesManagerService.AddFrame(device, jpeg);
            }
        }

        private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
                if (read <= 0)
                {
                    throw new EndOfStreamException();
                }
                offset += read;
            }
        }

        private static byte[] ExportUncompressedPoint(ECPoint q)
        {
            if (q.X == null || q.Y == null || q.X.Length != 32 || q.Y.Length != 32)
            {
                throw new InvalidOperationException("Unexpected P-256 point shape");
            }
            var result = new byte[1 + 32 + 32];
            result[0] = 0x04;
            Buffer.BlockCopy(q.X, 0, result, 1, 32);
            Buffer.BlockCopy(q.Y, 0, result, 33, 32);
            return result;
        }

        private static byte[] Concat(params byte[][] arrays)
        {
            var total = 0;
            foreach (var a in arrays) total += a.Length;
            var result = new byte[total];
            var offset = 0;
            foreach (var a in arrays)
            {
                Buffer.BlockCopy(a, 0, result, offset, a.Length);
                offset += a.Length;
            }
            return result;
        }

        private static int ParseIntOrDefault(string? value, int fallback)
        {
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static long ParseLongOrDefault(string? value, long fallback)
        {
            return long.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private sealed class Session
        {
            public required byte[] SessionKey { get; init; }
            public required byte[] IvBase { get; init; }
            public required byte[] SessionId { get; init; }
            public required byte[] DeviceIdBytes { get; init; }
            public required DateTime StartedAt { get; init; }
        }
    }
}
