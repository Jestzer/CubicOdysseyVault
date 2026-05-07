using Avalonia;
using Avalonia.Media.Imaging;
using CubicOdysseyVault.Core.SaveContent;

namespace CubicOdysseyVault.UI.Services;

// Process-wide cache for atlas Bitmaps. Decoding a 2048×2048 PNG is ~50 MB in
// memory; we want exactly one instance per atlas path. The cache is keyed on
// canonical absolute path so different Save Inspector openings share state.
public static class IconAtlasCache
{
    private static readonly Dictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    public static Bitmap? GetOrLoad(string? atlasPath)
    {
        if (string.IsNullOrEmpty(atlasPath)) return null;
        lock (_lock)
        {
            if (_cache.TryGetValue(atlasPath, out var cached)) return cached;
            Bitmap? bmp = null;
            try { bmp = new Bitmap(atlasPath); } catch { /* swallow */ }
            _cache[atlasPath] = bmp;
            return bmp;
        }
    }

    public static CroppedBitmap? Slice(SpriteAtlas? atlas, int frameIndex)
    {
        if (atlas == null) return null;
        var frame = atlas.Lookup(frameIndex);
        if (frame == null) return null;
        var atlasBmp = GetOrLoad(atlas.AtlasImagePath);
        if (atlasBmp == null) return null;

        // Defensive clamp — if the bspr frame somehow extends past the
        // atlas, clip it rather than throwing.
        int x = Math.Clamp(frame.X, 0, atlasBmp.PixelSize.Width);
        int y = Math.Clamp(frame.Y, 0, atlasBmp.PixelSize.Height);
        int w = Math.Clamp(frame.Width, 1, atlasBmp.PixelSize.Width - x);
        int h = Math.Clamp(frame.Height, 1, atlasBmp.PixelSize.Height - y);

        try { return new CroppedBitmap(atlasBmp, new PixelRect(x, y, w, h)); }
        catch { return null; }
    }
}
