using CubicOdysseyVault.Core.Tga;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class TgaDecoderTests
{
    [Fact]
    public void Decode_2x2_24bpp_BottomUp_SwapsBgrToRgbAndFlipsRows()
    {
        // 2x2, 24bpp, bottom-up. TGA stores rows bottom-first.
        // We construct the file so that after decoding the top row reads RGB pixels P1, P2
        // and the bottom row reads P3, P4.
        var pixels = new byte[]
        {
            // Bottom row first (TGA's natural order for bottom-up).
            // Pixel: 0xR3 0xG3 0xB3 stored as B,G,R = 0xB3, 0xG3, 0xR3
            0xB3, 0xA3, 0x93,  0xB4, 0xA4, 0x94,
            // Top row second
            0xB1, 0xA1, 0x91,  0xB2, 0xA2, 0x92,
        };
        var data = BuildTga(width: 2, height: 2, bpp: 24, topDown: false, pixelData: pixels);

        var image = TgaDecoder.Decode(data);
        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(16, image.RgbaPixels.Length);

        // Top-left after decode = "Pixel 1": R=0x91, G=0xA1, B=0xB1, A=0xFF
        Assert.Equal(0x91, image.RgbaPixels[0]);
        Assert.Equal(0xA1, image.RgbaPixels[1]);
        Assert.Equal(0xB1, image.RgbaPixels[2]);
        Assert.Equal(0xFF, image.RgbaPixels[3]);

        // Top-right "Pixel 2"
        Assert.Equal(0x92, image.RgbaPixels[4]);
        Assert.Equal(0xA2, image.RgbaPixels[5]);
        Assert.Equal(0xB2, image.RgbaPixels[6]);

        // Bottom-left "Pixel 3"
        Assert.Equal(0x93, image.RgbaPixels[8]);
        Assert.Equal(0xA3, image.RgbaPixels[9]);
        Assert.Equal(0xB3, image.RgbaPixels[10]);
    }

    [Fact]
    public void Decode_2x1_32bpp_TopDown_PreservesRowOrder_AndAlpha()
    {
        var pixels = new byte[]
        {
            0xB1, 0xA1, 0x91, 0x80,  // pixel 1: B=0xB1, G=0xA1, R=0x91, A=0x80
            0xB2, 0xA2, 0x92, 0xC0,
        };
        var data = BuildTga(width: 2, height: 1, bpp: 32, topDown: true, pixelData: pixels);

        var image = TgaDecoder.Decode(data);
        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);

        Assert.Equal(0x91, image.RgbaPixels[0]);
        Assert.Equal(0xA1, image.RgbaPixels[1]);
        Assert.Equal(0xB1, image.RgbaPixels[2]);
        Assert.Equal(0x80, image.RgbaPixels[3]);
        Assert.Equal(0xC0, image.RgbaPixels[7]);
    }

    [Fact]
    public void Decode_24bpp_AlphaIsForcedTo_FF()
    {
        var pixels = new byte[] { 0x10, 0x20, 0x30 };
        var data = BuildTga(width: 1, height: 1, bpp: 24, topDown: true, pixelData: pixels);

        var image = TgaDecoder.Decode(data);
        Assert.Equal(0xFF, image.RgbaPixels[3]);
    }

    [Fact]
    public void Decode_TooShort_Throws()
    {
        var tiny = new byte[] { 0, 0, 2, 0 };
        Assert.Throws<InvalidDataException>(() => TgaDecoder.Decode(tiny));
    }

    [Fact]
    public void Decode_RleImageType_Throws()
    {
        var data = BuildTga(width: 1, height: 1, bpp: 24, topDown: true, pixelData: new byte[3]);
        data[2] = 10; // RLE RGB
        Assert.Throws<NotSupportedException>(() => TgaDecoder.Decode(data));
    }

    [Fact]
    public void Decode_TruncatedPixelData_Throws()
    {
        var data = BuildTga(width: 4, height: 4, bpp: 32, topDown: true, pixelData: new byte[10]);
        Assert.Throws<InvalidDataException>(() => TgaDecoder.Decode(data));
    }

    [Fact]
    public void TryDecodeFile_OnMissingFile_ReturnsNull()
    {
        var result = TgaDecoder.TryDecodeFile(Path.Combine(Path.GetTempPath(), $"covtest-missing-{Guid.NewGuid():N}.tga"));
        Assert.Null(result);
    }

    [Fact]
    public void Decode_RealCubicOdysseyHeaderShape_Decodes()
    {
        // Mirror the real screenshot.tga shape on this user's machine: 512x214, 32 bpp,
        // bottom-up, image_type 2, no id field, no color map. Just verify it decodes;
        // pixel content is zeroed but still tests the shape.
        var pixels = new byte[512 * 214 * 4];
        var data = BuildTga(width: 512, height: 214, bpp: 32, topDown: false, pixelData: pixels);
        var image = TgaDecoder.Decode(data);
        Assert.Equal(512, image.Width);
        Assert.Equal(214, image.Height);
    }

    private static byte[] BuildTga(int width, int height, int bpp, bool topDown, byte[] pixelData)
    {
        var data = new byte[18 + pixelData.Length];
        // header
        data[2] = 2; // uncompressed RGB
        data[12] = (byte)(width & 0xFF);
        data[13] = (byte)((width >> 8) & 0xFF);
        data[14] = (byte)(height & 0xFF);
        data[15] = (byte)((height >> 8) & 0xFF);
        data[16] = (byte)bpp;
        data[17] = (byte)((topDown ? 0x20 : 0x00) | (bpp == 32 ? 0x08 : 0x00));
        Buffer.BlockCopy(pixelData, 0, data, 18, pixelData.Length);
        return data;
    }
}
