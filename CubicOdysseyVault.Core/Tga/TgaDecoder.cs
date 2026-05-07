namespace CubicOdysseyVault.Core.Tga;

// Minimal uncompressed-RGB TGA decoder. Cubic Odyssey screenshots on this
// machine are 32 bpp BGRA, bottom-up, image_type 2 — verified by inspection.
// RLE (image_type 10) is intentionally not supported yet; revisit if a save
// surfaces an RLE TGA. Color-mapped (1, 9) and grayscale (3, 11) likewise
// out of scope.
public static class TgaDecoder
{
    private const int HeaderSize = 18;

    public static TgaImage Decode(byte[] data)
    {
        if (data.Length < HeaderSize)
            throw new InvalidDataException("TGA file shorter than 18-byte header.");

        byte idLength = data[0];
        byte colorMapType = data[1];
        byte imageType = data[2];
        int colorMapLength = data[5] | (data[6] << 8);
        byte colorMapEntrySize = data[7];
        int width = data[12] | (data[13] << 8);
        int height = data[14] | (data[15] << 8);
        byte bpp = data[16];
        byte descriptor = data[17];

        if (imageType != 2)
            throw new NotSupportedException(
                $"TGA image_type {imageType} not supported (only uncompressed RGB / image_type 2).");
        if (bpp != 24 && bpp != 32)
            throw new NotSupportedException($"TGA {bpp} bpp not supported (only 24 or 32).");
        if (width <= 0 || height <= 0)
            throw new InvalidDataException($"TGA invalid dimensions {width}x{height}.");

        bool topDown = (descriptor & 0x20) != 0;
        int bytesPerPixel = bpp / 8;

        int colorMapBytes = colorMapType == 1
            ? colorMapLength * (colorMapEntrySize / 8)
            : 0;

        int dataOffset = HeaderSize + idLength + colorMapBytes;
        long expectedDataSize = (long)width * height * bytesPerPixel;
        if (data.Length - dataOffset < expectedDataSize)
            throw new InvalidDataException(
                $"TGA pixel data truncated (need {expectedDataSize}, have {data.Length - dataOffset}).");

        var rgba = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            int srcRow = topDown ? y : (height - 1 - y);
            int srcLineStart = dataOffset + srcRow * width * bytesPerPixel;
            int dstLineStart = y * width * 4;

            for (int x = 0; x < width; x++)
            {
                int s = srcLineStart + x * bytesPerPixel;
                int d = dstLineStart + x * 4;

                // TGA stores BGR(A); we emit RGBA.
                rgba[d + 0] = data[s + 2];
                rgba[d + 1] = data[s + 1];
                rgba[d + 2] = data[s + 0];
                rgba[d + 3] = bpp == 32 ? data[s + 3] : (byte)0xFF;
            }
        }

        return new TgaImage(width, height, rgba);
    }

    public static TgaImage DecodeFile(string path) => Decode(File.ReadAllBytes(path));

    public static TgaImage? TryDecodeFile(string path)
    {
        try { return DecodeFile(path); }
        catch { return null; }
    }
}
