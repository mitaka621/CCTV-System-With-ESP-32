using System.Globalization;
using System.Text;

namespace CamPortal.Core.Utilities
{
    public class MultipartJpegStreamReader
    {
        private const string _contentLengthHeader = "Content-Length:";
        private const int _maximumHeaderLineLength = 1024;
        private const int _maximumFrameLength = 64 * 1024 * 1024;

        private readonly Stream _stream;
        private readonly byte[] _buffer = new byte[16 * 1024];
        private int _bufferStart;
        private int _bufferEnd;

        public MultipartJpegStreamReader(Stream stream)
        {
            _stream = stream;
        }

        public async Task<byte[]?> ReadFrameAsync(CancellationToken cancellationToken)
        {
            int? contentLength = null;

            while (true)
            {
                var line = await ReadLineAsync(cancellationToken);

                if (line == null)
                {
                    return null;
                }

                if (line.Length == 0)
                {
                    if (contentLength != null)
                    {
                        break;
                    }

                    continue;
                }

                if (line.StartsWith(_contentLengthHeader, StringComparison.OrdinalIgnoreCase))
                {
                    var value = line[_contentLengthHeader.Length..].Trim();

                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var length)
                        || length <= 0
                        || length > _maximumFrameLength)
                    {
                        throw new InvalidDataException($"Invalid multipart content length '{value}'.");
                    }

                    contentLength = length;
                }
            }

            var frame = new byte[contentLength.Value];

            return await ReadExactlyAsync(frame, cancellationToken) ? frame : null;
        }

        private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            var line = new StringBuilder();

            while (true)
            {
                if (_bufferStart == _bufferEnd && !await FillBufferAsync(cancellationToken))
                {
                    return line.Length > 0 ? line.ToString() : null;
                }

                var character = (char)_buffer[_bufferStart++];

                if (character == '\n')
                {
                    return line.ToString().TrimEnd('\r');
                }

                if (line.Length >= _maximumHeaderLineLength)
                {
                    throw new InvalidDataException("Multipart header line is too long.");
                }

                line.Append(character);
            }
        }

        private async Task<bool> FillBufferAsync(CancellationToken cancellationToken)
        {
            _bufferStart = 0;
            _bufferEnd = await _stream.ReadAsync(_buffer, cancellationToken);

            return _bufferEnd > 0;
        }

        private async Task<bool> ReadExactlyAsync(byte[] destination, CancellationToken cancellationToken)
        {
            var bufferedLength = Math.Min(_bufferEnd - _bufferStart, destination.Length);

            Buffer.BlockCopy(_buffer, _bufferStart, destination, 0, bufferedLength);
            _bufferStart += bufferedLength;

            var offset = bufferedLength;

            while (offset < destination.Length)
            {
                var readLength = await _stream.ReadAsync(destination.AsMemory(offset), cancellationToken);

                if (readLength == 0)
                {
                    return false;
                }

                offset += readLength;
            }

            return true;
        }
    }
}
