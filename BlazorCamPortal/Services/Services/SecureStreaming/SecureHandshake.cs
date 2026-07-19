using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.DeviceDtos;
using CamPortal.Contracts.Dtos.SecureStreamingDtos;
using CamPortal.Contracts.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CamPortal.Core.Services.SecureStreaming
{
    public class SecureHandshake : ISecureHandshake
    {
        private const string _domainTag = "CAMPR-STREAM-V1";
        private const string _hkdfInfo = "CAMPR-STREAM-V1-derived";
        private const int _deviceIdLen = 16;
        private const int _nonceLen = 32;
        private const int _ephemeralPubLen = 65;
        private const int _signatureLen = 64;
        private const int _ivBaseLen = 4;
        private const int _sessionIdLen = 16;

        private readonly ILogger<SecureHandshake> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServerIdentityService _serverIdentityService;
        private readonly IConfiguration _configuration;
        private readonly int _handshakeTimeoutSeconds;

        public SecureHandshake(
            ILogger<SecureHandshake> logger,
            IServiceProvider serviceProvider,
            IServerIdentityService serverIdentityService,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _serverIdentityService = serverIdentityService;
            _configuration = configuration;

            _handshakeTimeoutSeconds = int.Parse(
                configuration.GetSection("SecureStreaming")["HandshakeTimeoutSeconds"]
                ?? throw new ArgumentNullException("Handshake timeout not configured"));
        }

        public async Task<DeviceStreamingHandshakeDto?> AuthorizeAsync(Stream stream, EndPoint? remoteEndpoint, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_handshakeTimeoutSeconds));

            var deviceIdBytes = new byte[_deviceIdLen];
            await ReadExactAsync(stream, deviceIdBytes, timeoutCts.Token);

            var deviceId = new Guid(deviceIdBytes, bigEndian: true);

            DeviceStreamingHandshakeDto? device;
            using (var scope = _serviceProvider.CreateScope())
            {
                var deviceRepository = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                device = await deviceRepository.GetDeviceForStreamingHandshakeAsync(deviceId);
            }

            if (device == null)
            {
                _logger.LogWarning("Stream rejected from {RemoteEndpoint}: device {DeviceId} not found", remoteEndpoint, deviceId);
                return null;
            }

            if (device.PairStatus != DevicePairStatus.Paired)
            {
                _logger.LogWarning(
                    "Stream rejected from {RemoteEndpoint}: device {DeviceId} not paired (status={Status})",
                    remoteEndpoint, deviceId, device.PairStatus);
                return null;
            }

            return device;
        }

        public async Task<ISecureChannel?> EstablishAsync(Stream stream, DeviceStreamingHandshakeDto device, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_handshakeTimeoutSeconds));

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

            const int derivedLen = 32 + _ivBaseLen + _sessionIdLen + 32 + _ivBaseLen;
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
            var downstreamKey = new byte[32];
            var downstreamIvBase = new byte[_ivBaseLen];
            Buffer.BlockCopy(derivedKeyMaterial, 0, sessionKey, 0, 32);
            Buffer.BlockCopy(derivedKeyMaterial, 32, ivBase, 0, _ivBaseLen);
            Buffer.BlockCopy(derivedKeyMaterial, 32 + _ivBaseLen, sessionId, 0, _sessionIdLen);
            Buffer.BlockCopy(derivedKeyMaterial, 32 + _ivBaseLen + _sessionIdLen, downstreamKey, 0, 32);
            Buffer.BlockCopy(derivedKeyMaterial, 32 + _ivBaseLen + _sessionIdLen + 32, downstreamIvBase, 0, _ivBaseLen);
            CryptographicOperations.ZeroMemory(derivedKeyMaterial);

            _logger.LogInformation(
                "Stream {DeviceId}: handshake complete, session {SessionId}",
                device.Id,
                Convert.ToHexString(sessionId));

            var sessionMaterial = new SecureSessionMaterialDto
            {
                SessionKey = sessionKey,
                IvBase = ivBase,
                DownstreamKey = downstreamKey,
                DownstreamIvBase = downstreamIvBase,
                SessionId = sessionId,
                DeviceIdBytes = deviceIdBytes,
                StartedAt = DateTime.UtcNow,
            };

            return new SecureChannel(stream, sessionMaterial, _configuration);
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
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
    }
}
