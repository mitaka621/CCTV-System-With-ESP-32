using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.SecureStreamingDtos;
using CamPortal.Contracts.Exceptions;
using Microsoft.Extensions.Configuration;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CamPortal.Core.Services.SecureStreaming
{
    public sealed class SecureChannel : ISecureChannel
    {
        private const int _gcmTagLen = 16;
        private const int _gcmIvLen = 12;
        private const int _seqLen = 8;
        private const int _ivBaseLen = 4;
        private const int _sessionIdLen = 16;
        private const int _deviceIdLen = 16;
        private const int _frameHeaderLen = 4 + _seqLen + _gcmTagLen;

        private readonly Stream _stream;
        private readonly AesGcm _aesGcm;
        private readonly AesGcm _downstreamAesGcm;
        private readonly int _maxFrameBytes;
        private readonly int _replayWindow;

        private readonly byte[] _headerBuffer = new byte[_frameHeaderLen];
        private readonly byte[] _ivBuffer = new byte[_gcmIvLen];
        private readonly byte[] _aadBuffer = new byte[_sessionIdLen + _deviceIdLen + _seqLen];

        private readonly byte[] _sendIvBuffer = new byte[_gcmIvLen];
        private readonly byte[] _sendAadBuffer = new byte[_sessionIdLen + _deviceIdLen + _seqLen];

        private ulong _lastSeq;
        private ulong _sendSeq;

        public SecureChannel(Stream stream, SecureSessionMaterialDto sessionMaterial, IConfiguration configuration)
        {
            _stream = stream;

            var streamingSection = configuration.GetSection("SecureStreaming");

            _maxFrameBytes = int.Parse(
                streamingSection["MaxFrameBytes"]
                ?? throw new ArgumentNullException("Max frame bytes not configured"));

            _replayWindow = int.Parse(
                streamingSection["ReplayWindow"]
                ?? throw new ArgumentNullException("Replay window not configured"));

            _aesGcm = new AesGcm(sessionMaterial.SessionKey, _gcmTagLen);
            _downstreamAesGcm = new AesGcm(sessionMaterial.DownstreamKey, _gcmTagLen);

            Buffer.BlockCopy(sessionMaterial.IvBase, 0, _ivBuffer, 0, _ivBaseLen);
            Buffer.BlockCopy(sessionMaterial.SessionId, 0, _aadBuffer, 0, _sessionIdLen);
            Buffer.BlockCopy(sessionMaterial.DeviceIdBytes, 0, _aadBuffer, _sessionIdLen, _deviceIdLen);

            Buffer.BlockCopy(sessionMaterial.DownstreamIvBase, 0, _sendIvBuffer, 0, _ivBaseLen);
            Buffer.BlockCopy(sessionMaterial.SessionId, 0, _sendAadBuffer, 0, _sessionIdLen);
            Buffer.BlockCopy(sessionMaterial.DeviceIdBytes, 0, _sendAadBuffer, _sessionIdLen, _deviceIdLen);
        }

        public async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
        {
            await ReadExactAsync(_headerBuffer, cancellationToken);

            var totalLen = BinaryPrimitives.ReadUInt32BigEndian(_headerBuffer.AsSpan(0, 4));
            var seq = BinaryPrimitives.ReadUInt64BigEndian(_headerBuffer.AsSpan(4, _seqLen));

            var minTotalLen = (uint)(_seqLen + _gcmTagLen + 1);
            var maxTotalLen = (uint)(_seqLen + _gcmTagLen + _maxFrameBytes);
            if (totalLen < minTotalLen || totalLen > maxTotalLen)
            {
                throw new SecureChannelProtocolException($"Invalid frame total length {totalLen}");
            }

            if (seq == 0)
            {
                throw new SecureChannelProtocolException("Sequence 0 is reserved");
            }

            if (seq <= _lastSeq)
            {
                throw new SecureChannelProtocolException($"Replay/out-of-order sequence {seq} (last {_lastSeq})");
            }

            if (seq - _lastSeq > (ulong)_replayWindow)
            {
                throw new SecureChannelProtocolException($"Sequence gap too large {seq} (last {_lastSeq})");
            }

            var tag = new byte[_gcmTagLen];
            Buffer.BlockCopy(_headerBuffer, _frameHeaderLen - _gcmTagLen, tag, 0, _gcmTagLen);

            var ciphertextLen = (int)(totalLen - _seqLen - _gcmTagLen);
            var ciphertext = new byte[ciphertextLen];
            await ReadExactAsync(ciphertext, cancellationToken);

            BinaryPrimitives.WriteUInt64BigEndian(_ivBuffer.AsSpan(_ivBaseLen, _seqLen), seq);
            BinaryPrimitives.WriteUInt64BigEndian(_aadBuffer.AsSpan(_sessionIdLen + _deviceIdLen, _seqLen), seq);

            var plaintext = new byte[ciphertextLen];
            _aesGcm.Decrypt(_ivBuffer, ciphertext, tag, plaintext, _aadBuffer);

            _lastSeq = seq;
            return plaintext;
        }

        public async Task SendAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken)
        {
            if (plaintext.Length == 0)
            {
                throw new ArgumentException("Cannot send an empty payload", nameof(plaintext));
            }

            _sendSeq++;
            var seq = _sendSeq;

            BinaryPrimitives.WriteUInt64BigEndian(_sendIvBuffer.AsSpan(_ivBaseLen, _seqLen), seq);
            BinaryPrimitives.WriteUInt64BigEndian(_sendAadBuffer.AsSpan(_sessionIdLen + _deviceIdLen, _seqLen), seq);

            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[_gcmTagLen];
            _downstreamAesGcm.Encrypt(_sendIvBuffer, plaintext.Span, ciphertext, tag, _sendAadBuffer);

            var totalLen = (uint)(_seqLen + _gcmTagLen + ciphertext.Length);
            var header = new byte[_frameHeaderLen];
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), totalLen);
            BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(4, _seqLen), seq);
            Buffer.BlockCopy(tag, 0, header, _frameHeaderLen - _gcmTagLen, _gcmTagLen);

            await _stream.WriteAsync(header, cancellationToken);
            await _stream.WriteAsync(ciphertext, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }

        private async Task ReadExactAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await _stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
                if (read <= 0)
                {
                    throw new EndOfStreamException();
                }
                offset += read;
            }
        }

        public ValueTask DisposeAsync()
        {
            _aesGcm.Dispose();
            _downstreamAesGcm.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
