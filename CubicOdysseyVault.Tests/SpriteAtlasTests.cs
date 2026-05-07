using CubicOdysseyVault.Core.SaveContent;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class SpriteAtlasTests
{
    [Fact]
    public void LoadFromGameInstall_NonExistent_ReturnsNull()
    {
        var atlas = SpriteAtlas.LoadFromGameInstall(Path.Combine(Path.GetTempPath(), $"covtest-no-{Guid.NewGuid():N}"));
        Assert.Null(atlas);
    }

    [Fact]
    public void LoadFromGameInstall_DirWithoutSprites_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"covtest-empty-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            Assert.Null(SpriteAtlas.LoadFromGameInstall(dir));
        }
        finally { try { Directory.Delete(dir); } catch { } }
    }

    [Fact]
    public void LoadFromGameInstall_SyntheticBspr_ParsesFrames()
    {
        // Build a minimal install dir with a fake bspr + dummy png.
        var dir = Path.Combine(Path.GetTempPath(), $"covtest-atlas-{Guid.NewGuid():N}");
        var spritesDir = Path.Combine(dir, "data", "sprites");
        Directory.CreateDirectory(spritesDir);
        try
        {
            // Synthetic bspr with 3 records: zero-rect (header), tiny rect, real rect.
            var bspr = new List<byte>
            {
                // BSPR magic + version
                (byte)'B', (byte)'S', (byte)'P', (byte)'R',
                0x08, 0x00, 0x00, 0x00,
                // Frame 0: reserved=non-zero, treated as null
                0x01, 0x00, 0x00, 0x00,  0, 0,  0, 0,  0, 0,  0, 0,
                // Frame 1: w=h=0, treated as null
                0x00, 0x00, 0x00, 0x00,  10, 0,  10, 0,  0, 0,  0, 0,
                // Frame 2: real rect — x=100, y=200, w=50, h=60
                0x00, 0x00, 0x00, 0x00,  0x64, 0x00,  0xc8, 0x00,  0x32, 0x00,  0x3c, 0x00,
            };
            // append icons.png string
            bspr.AddRange(System.Text.Encoding.ASCII.GetBytes("icons.png"));
            bspr.Add(0);
            File.WriteAllBytes(Path.Combine(spritesDir, "icons.bspr"), bspr.ToArray());
            File.WriteAllBytes(Path.Combine(spritesDir, "icons.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // not a real PNG, but file must exist

            var atlas = SpriteAtlas.LoadFromGameInstall(dir);
            Assert.NotNull(atlas);
            Assert.Null(atlas!.Lookup(0));   // header-ish
            Assert.Null(atlas.Lookup(1));    // tiny / zero rect
            var f2 = atlas.Lookup(2);
            Assert.NotNull(f2);
            Assert.Equal(100, f2!.X);
            Assert.Equal(200, f2.Y);
            Assert.Equal(50, f2.Width);
            Assert.Equal(60, f2.Height);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void Lookup_OutOfRange_ReturnsNull()
    {
        var atlas = new SpriteAtlas("/dev/null", new SpriteFrame?[] { new(0, 0, 1, 1) });
        Assert.Null(atlas.Lookup(-1));
        Assert.Null(atlas.Lookup(99));
    }
}
