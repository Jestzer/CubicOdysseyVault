using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CubicOdysseyVault.Core.Tga;

namespace CubicOdysseyVault.UI.Services;

public static class TgaBitmapLoader
{
    public static Bitmap? TryLoad(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var img = TgaDecoder.TryDecodeFile(path);
        return img == null ? null : ToBitmap(img);
    }

    public static Bitmap ToBitmap(TgaImage img)
    {
        var bmp = new WriteableBitmap(
            new PixelSize(img.Width, img.Height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Unpremul);

        using var fb = bmp.Lock();
        int srcStride = img.Width * 4;
        int dstStride = fb.RowBytes;

        if (srcStride == dstStride)
        {
            Marshal.Copy(img.RgbaPixels, 0, fb.Address, img.RgbaPixels.Length);
        }
        else
        {
            for (int y = 0; y < img.Height; y++)
                Marshal.Copy(img.RgbaPixels, y * srcStride, fb.Address + y * dstStride, srcStride);
        }

        return bmp;
    }
}
