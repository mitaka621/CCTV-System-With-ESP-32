using System.Buffers.Binary;

namespace Homewatch.Core.Utilities
{
    public static class JpegHeaderReader
    {
        public static bool TryReadDimensions(ReadOnlySpan<byte> jpeg, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            {
                return false;
            }

            var position = 2;

            while (position + 4 <= jpeg.Length)
            {
                if (jpeg[position] != 0xFF)
                {
                    return false;
                }

                var marker = jpeg[position + 1];

                if (marker == 0xFF)
                {
                    position++;
                    continue;
                }

                if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    position += 2;
                    continue;
                }

                if (marker == 0xD9 || marker == 0xDA)
                {
                    return false;
                }

                if (IsStartOfFrameMarker(marker))
                {
                    if (position + 9 > jpeg.Length)
                    {
                        return false;
                    }

                    height = BinaryPrimitives.ReadUInt16BigEndian(jpeg.Slice(position + 5, 2));
                    width = BinaryPrimitives.ReadUInt16BigEndian(jpeg.Slice(position + 7, 2));

                    return width > 0 && height > 0;
                }

                position += 2 + BinaryPrimitives.ReadUInt16BigEndian(jpeg.Slice(position + 2, 2));
            }

            return false;
        }

        private static bool IsStartOfFrameMarker(byte marker)
        {
            return marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
        }
    }
}
