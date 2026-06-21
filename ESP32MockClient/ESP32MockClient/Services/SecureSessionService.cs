using ESP32MockClient.Configuration;
using ESP32MockClient.Models;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ESP32MockClient.Services;

public class SecureSessionService : IDisposable
{
    private const string _streamDomainTag = "CAMPR-STREAM-V1";
    private const string _streamHkdfInfo = "CAMPR-STREAM-V1-derived";
    private const int _handshakeTimeoutMs = 10000;
    private const int _serverHelloLength = 32 + 65 + 64;

    private readonly VideoFrameLoader _frameLoader;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private AesGcm? _aesGcm;

    private byte[] _deviceIdRaw = [];
    private byte[] _ivBase = [];
    private byte[] _sessionId = [];
    private ulong _seq;

    private int _failedConnectionAttempts;

    public SecureSessionService(VideoFrameLoader frameLoader)
    {
        _frameLoader = frameLoader;
    }

    public async Task RunStreamingLoopAsync(PreprovisionCredentials credentials, CancellationToken ct)
    {
        var (width, height) = ParseResolution(MockClientConfiguration.Resolution);
        var frameCount = 0;
        var startTime = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            if (!IsSessionActive())
            {
                if (!StartSession(credentials))
                {
                    _failedConnectionAttempts++;
                    EndSession();

                    if (_failedConnectionAttempts >= MockClientConfiguration.MaxFailedConnectionAttempts)
                    {
                        break;
                    }

                    await Task.Delay(5000, ct);
                    continue;
                }

                _failedConnectionAttempts = 0;
                frameCount = 0;
                startTime = DateTime.UtcNow;
            }

            var frame = _frameLoader.GetNextFrame();
            if (frame == null)
            {
                await Task.Delay(100, ct);
                continue;
            }

            if (!SendFrame(frame, width, height))
            {
                EndSession();
            }
            else
            {
                frameCount++;
                if (frameCount % 100 == 0)
                {
                    var fps = frameCount / (DateTime.UtcNow - startTime).TotalSeconds;
                    Console.WriteLine($"[Stream] {frameCount} frames ({fps:F1} fps)");
                }
            }

            _frameLoader.ReturnFrame();
            await Task.Delay(MockClientConfiguration.FrameDelayMs, ct);
        }

        EndSession();
    }

    private bool StartSession(PreprovisionCredentials credentials)
    {
        try
        {
            _tcpClient = new TcpClient { NoDelay = true };
            _tcpClient.Connect(credentials.ServerIp, MockClientConfiguration.ServerTcpPort);
            _stream = _tcpClient.GetStream();
            _stream.ReadTimeout = _handshakeTimeoutMs;

            if (!PerformHandshake(credentials))
            {
                Console.WriteLine("[Stream] Handshake failed");
                return false;
            }

            Console.WriteLine("[Stream] Secure session established");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Stream] Connection failed: {ex.Message}");
            return false;
        }
    }

    private bool PerformHandshake(PreprovisionCredentials credentials)
    {
        if (_stream == null)
        {
            return false;
        }

        _deviceIdRaw = Convert.FromHexString(credentials.DeviceId.ToString("N"));

        WriteAll(_deviceIdRaw);

        var serverHello = ReadExact(_serverHelloLength);
        var nonceServer = serverHello[..32];
        var ephemeralServerPub = serverHello[32..97];
        var serverSignature = serverHello[97..161];

        if (ephemeralServerPub[0] != 0x04)
        {
            Console.WriteLine("[Stream] Server ephemeral public key format invalid");
            return false;
        }

        var domain = DomainWithNullTerminator(_streamDomainTag);

        var serverSignatureHash = SHA256.HashData(Concat(domain, _deviceIdRaw, nonceServer, ephemeralServerPub));
        if (!VerifyServerSignature(credentials.ServerIdentityPublicKey, serverSignatureHash, serverSignature))
        {
            Console.WriteLine("[Stream] Server signature INVALID");
            return false;
        }

        using var deviceEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var ephemeralDevicePub = ExportUncompressedPublicKey(deviceEcdh);
        var sharedSecret = DeriveSharedSecret(deviceEcdh, ephemeralServerPub);

        var nonceDevice = RandomNumberGenerator.GetBytes(32);

        var deviceSignatureHash = SHA256.HashData(
            Concat(domain, _deviceIdRaw, nonceServer, nonceDevice, ephemeralServerPub, ephemeralDevicePub));
        var deviceSignature = SignDeviceHello(credentials.PrivateKey, deviceSignatureHash);

        WriteAll(Concat(nonceDevice, ephemeralDevicePub, deviceSignature));

        var salt = Concat(nonceServer, nonceDevice);
        var info = Encoding.ASCII.GetBytes(_streamHkdfInfo);
        var keyMaterial = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32 + 4 + 16, salt, info);

        var sessionKey = keyMaterial[..32];
        _ivBase = keyMaterial[32..36];
        _sessionId = keyMaterial[36..52];
        _seq = 0;

        _aesGcm?.Dispose();
        _aesGcm = new AesGcm(sessionKey, 16);

        return true;
    }

    private bool SendFrame(byte[] jpeg, uint width, uint height)
    {
        if (_stream == null || _aesGcm == null)
        {
            return false;
        }

        try
        {
            var plaintext = new byte[8 + jpeg.Length];
            BinaryPrimitives.WriteUInt32BigEndian(plaintext.AsSpan(0, 4), width);
            BinaryPrimitives.WriteUInt32BigEndian(plaintext.AsSpan(4, 4), height);
            Buffer.BlockCopy(jpeg, 0, plaintext, 8, jpeg.Length);

            _seq++;

            var iv = new byte[12];
            Buffer.BlockCopy(_ivBase, 0, iv, 0, 4);
            BinaryPrimitives.WriteUInt64BigEndian(iv.AsSpan(4, 8), _seq);

            var aad = new byte[16 + 16 + 8];
            Buffer.BlockCopy(_sessionId, 0, aad, 0, 16);
            Buffer.BlockCopy(_deviceIdRaw, 0, aad, 16, 16);
            BinaryPrimitives.WriteUInt64BigEndian(aad.AsSpan(32, 8), _seq);

            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            _aesGcm.Encrypt(iv, plaintext, ciphertext, tag, aad);

            var totalLen = (uint)(8 + tag.Length + ciphertext.Length);
            var header = new byte[4 + 8 + 16];
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), totalLen);
            BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(4, 8), _seq);
            Buffer.BlockCopy(tag, 0, header, 12, 16);

            _stream.Write(header, 0, header.Length);
            _stream.Write(ciphertext, 0, ciphertext.Length);
            _stream.Flush();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Stream] Send failed: {ex.Message}");
            return false;
        }
    }

    private bool IsSessionActive()
    {
        return _aesGcm != null && (_tcpClient?.Connected ?? false);
    }

    private void EndSession()
    {
        _aesGcm?.Dispose();
        _aesGcm = null;

        _stream?.Close();
        _tcpClient?.Close();
        _stream = null;
        _tcpClient = null;
    }

    private void WriteAll(byte[] data)
    {
        _stream!.Write(data, 0, data.Length);
        _stream.Flush();
    }

    private byte[] ReadExact(int length)
    {
        var buffer = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var read = _stream!.Read(buffer, offset, length - offset);
            if (read <= 0)
            {
                throw new IOException("Connection closed during handshake");
            }
            offset += read;
        }

        return buffer;
    }

    private static bool VerifyServerSignature(string serverIdentityPublicKeyBase64, byte[] hash, byte[] signature)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(serverIdentityPublicKeyBase64), out _);

        return ecdsa.VerifyHash(hash, signature, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static byte[] SignDeviceHello(string privateKeyBase64, byte[] hash)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);

        return ecdsa.SignHash(hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static byte[] ExportUncompressedPublicKey(ECDiffieHellman ecdh)
    {
        var parameters = ecdh.ExportParameters(false);

        var result = new byte[65];
        result[0] = 0x04;
        Buffer.BlockCopy(LeftPad(parameters.Q.X!, 32), 0, result, 1, 32);
        Buffer.BlockCopy(LeftPad(parameters.Q.Y!, 32), 0, result, 33, 32);

        return result;
    }

    private static byte[] DeriveSharedSecret(ECDiffieHellman deviceEcdh, byte[] serverUncompressedPublicKey)
    {
        var serverParameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = serverUncompressedPublicKey[1..33],
                Y = serverUncompressedPublicKey[33..65]
            }
        };

        using var serverEcdh = ECDiffieHellman.Create(serverParameters);
        return deviceEcdh.DeriveRawSecretAgreement(serverEcdh.PublicKey);
    }

    private static byte[] DomainWithNullTerminator(string tag)
    {
        var ascii = Encoding.ASCII.GetBytes(tag);
        var result = new byte[ascii.Length + 1];
        Buffer.BlockCopy(ascii, 0, result, 0, ascii.Length);
        return result;
    }

    private static byte[] Concat(params byte[][] arrays)
    {
        var result = new byte[arrays.Sum(array => array.Length)];
        var offset = 0;

        foreach (var array in arrays)
        {
            Buffer.BlockCopy(array, 0, result, offset, array.Length);
            offset += array.Length;
        }

        return result;
    }

    private static byte[] LeftPad(byte[] value, int length)
    {
        if (value.Length == length)
        {
            return value;
        }

        var result = new byte[length];
        Buffer.BlockCopy(value, 0, result, length - value.Length, value.Length);
        return result;
    }

    private static (uint width, uint height) ParseResolution(string resolution)
    {
        var parts = resolution.Split('x');
        if (parts.Length == 2 && uint.TryParse(parts[0], out var width) && uint.TryParse(parts[1], out var height))
        {
            return (width, height);
        }

        return (1280, 720);
    }

    public void Dispose()
    {
        EndSession();
    }
}
