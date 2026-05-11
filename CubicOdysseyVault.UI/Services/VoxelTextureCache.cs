using System.Collections.Concurrent;
using CubicOdysseyVault.Core.Voxels;
using Pfim;

namespace CubicOdysseyVault.UI.Services;

// Lazy-loads each voxel material's "top" texture from the game install,
// decodes the DDS via Pfim, and downsamples it to a small BGRA tile that
// the map renderer can sample cheaply when drawing textured cells.
//
// Memory: ~290 materials × 64×64 × 4 bytes = ~5 MB if every texture ends
// up loaded. Each texture is decoded at most once and stays in the cache
// for the lifetime of the dialog.
//
// We sample the *top* face when present (m_defaultTextureTop), falling
// back to the side texture (m_defaultTexture). For top-down map rendering
// the top face is what the camera sees; the side texture is a reasonable
// approximation when the cfg doesn't differentiate.
public sealed class VoxelTextureCache
{
    public const int TileSize = 64;

    public VoxelTypeCatalog Catalog { get; }
    private readonly string? _gameInstallDir;
    // Sentinel `null` byte[] means "tried, failed, don't retry".
    private readonly ConcurrentDictionary<byte, byte[]?> _tiles = new();

    public VoxelTextureCache(VoxelTypeCatalog catalog)
    {
        Catalog = catalog;
        _gameInstallDir = catalog.GameInstallDir;
    }

    // Returns the BGRA tile for this voxel high-byte, or null when no
    // texture is available (catalog miss, missing file, decode failure).
    // Tile is always TileSize × TileSize × 4 bytes when non-null.
    public byte[]? GetTopTile(byte highByte)
    {
        if (_tiles.TryGetValue(highByte, out var cached)) return cached;

        var def = Catalog.Lookup(highByte);
        if (def is null) { _tiles[highByte] = null; return null; }

        var tile = LoadAndDownsample(def);
        _tiles[highByte] = tile;
        return tile;
    }

    private byte[]? LoadAndDownsample(VoxelDefinition def)
    {
        if (string.IsNullOrEmpty(_gameInstallDir)) return null;

        // Top texture wins; fall back to side. Both are bare names without
        // extension or directory in the cfg.
        var name = def.TopTexture ?? def.SideTexture;
        if (string.IsNullOrEmpty(name)) return null;

        var ddsPath = Path.Combine(_gameInstallDir, "data", "models", "voxels", name + ".dds");
        if (!File.Exists(ddsPath))
        {
            // Filesystems differ on case; resolve case-insensitively.
            ddsPath = ResolveCaseInsensitive(
                Path.Combine(_gameInstallDir, "data", "models", "voxels"),
                name + ".dds") ?? ddsPath;
            if (!File.Exists(ddsPath)) return null;
        }

        try
        {
            using var image = Pfimage.FromFile(ddsPath);
            return DownsampleToBgra(image, TileSize);
        }
        catch
        {
            return null;
        }
    }

    // Box-filter downsample to TileSize × TileSize, normalizing the source
    // pixel format to BGRA8888 along the way.
    private static byte[] DownsampleToBgra(IImage src, int target)
    {
        var dst = new byte[target * target * 4];
        var data = src.Data;
        int stride = src.Stride;
        int srcW = src.Width, srcH = src.Height;

        // Pfim returns Rgba32 / Rgb24 / Rgb24Bgr depending on the source
        // format. For voxel DDS files (BC3/DXT5) the decoded format is
        // Rgba32 with byte order BGRA — verified against sample files.
        // We tolerate Rgb24 (no alpha) by setting alpha to 255.
        bool isRgb24 = src.Format == ImageFormat.Rgb24;

        for (int ty = 0; ty < target; ty++)
        {
            int sy0 = (ty * srcH) / target;
            int sy1 = ((ty + 1) * srcH) / target;
            if (sy1 == sy0) sy1 = sy0 + 1;

            for (int tx = 0; tx < target; tx++)
            {
                int sx0 = (tx * srcW) / target;
                int sx1 = ((tx + 1) * srcW) / target;
                if (sx1 == sx0) sx1 = sx0 + 1;

                int sumB = 0, sumG = 0, sumR = 0, n = 0;
                for (int sy = sy0; sy < sy1; sy++)
                {
                    int rowOff = sy * stride;
                    for (int sx = sx0; sx < sx1; sx++)
                    {
                        int p = rowOff + sx * (isRgb24 ? 3 : 4);
                        sumB += data[p + 0];
                        sumG += data[p + 1];
                        sumR += data[p + 2];
                        n++;
                    }
                }

                int dstOff = (ty * target + tx) * 4;
                dst[dstOff + 0] = (byte)(sumB / n);
                dst[dstOff + 1] = (byte)(sumG / n);
                dst[dstOff + 2] = (byte)(sumR / n);
                dst[dstOff + 3] = 255;
            }
        }
        return dst;
    }

    private static string? ResolveCaseInsensitive(string dir, string name)
    {
        if (!Directory.Exists(dir)) return null;
        foreach (var path in Directory.EnumerateFiles(dir))
        {
            if (string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase))
                return path;
        }
        return null;
    }
}
