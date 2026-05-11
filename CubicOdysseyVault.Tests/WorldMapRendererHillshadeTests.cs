using System.Collections.Generic;
using System.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using CubicOdysseyVault.UI.Services;
using Xunit;

namespace CubicOdysseyVault.Tests;

// Hillshading should change the rendered output when there's actual slope,
// and leave it untouched when the surface is flat (no slope → uniform
// brightness no matter the algorithm).
public class WorldMapRendererHillshadeTests
{
    [AvaloniaFact]
    public void FlatPlateau_LooksTheSame_WithOrWithoutHillshade()
    {
        // 16×16 flat plateau at Y=10. Slope is zero everywhere, so the
        // hillshade multiplier should land at "flat = 1.0" and produce a
        // bitmap that's bit-identical to the no-hillshade render (within
        // bucket quantization, which both paths share).
        var voxels = new List<WorldMapRenderer.WorldVoxel>();
        for (int x = 0; x < 16; x++)
            for (int z = 0; z < 16; z++)
                voxels.Add(new WorldMapRenderer.WorldVoxel(x, 10, z, 0xAB000000u));

        var input = new WorldMapRenderer.Input(voxels);
        var withShade = WorldMapRenderer.RenderTopDown(input, 64, 64, hillshade: true);
        var withoutShade = WorldMapRenderer.RenderTopDown(input, 64, 64, hillshade: false);

        Assert.Equal(HashBitmap(withShade), HashBitmap(withoutShade));
    }

    [AvaloniaFact]
    public void Hill_LooksDifferent_WithHillshadeOnVsOff()
    {
        // Stair-stepped pyramid: heights rise toward the center, falling
        // off in every direction. There's slope on every cell except the
        // peak, so hillshading must produce visibly different output than
        // flat shading.
        var voxels = new List<WorldMapRenderer.WorldVoxel>();
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                int dx = System.Math.Abs(x - 8);
                int dz = System.Math.Abs(z - 8);
                int y = 16 - System.Math.Max(dx, dz);
                voxels.Add(new WorldMapRenderer.WorldVoxel(x, y, z, 0xAB000000u));
            }
        }
        var input = new WorldMapRenderer.Input(voxels);
        var withShade = WorldMapRenderer.RenderTopDown(input, 64, 64, hillshade: true);
        var withoutShade = WorldMapRenderer.RenderTopDown(input, 64, 64, hillshade: false);

        Assert.NotEqual(HashBitmap(withShade), HashBitmap(withoutShade));
    }

    private static int HashBitmap(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms);
        var data = ms.ToArray();
        unchecked
        {
            int h = (int)2166136261u;
            foreach (var b in data) { h = (h ^ b) * 16777619; }
            return h;
        }
    }
}
