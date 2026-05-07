namespace CubicOdysseyVault.Core.SaveContent;

public sealed record SpriteFrame(int X, int Y, int Width, int Height);

// Cubic Odyssey ships an icon atlas at <install>/data/sprites/icons.png plus
// metadata at <install>/data/sprites/icons.bspr ("BSPR" magic) describing
// each frame's rectangle within the atlas. Empirical layout:
//
//   bytes 0..3   "BSPR"
//   bytes 4..7   version u32 (always 8 in samples)
//   bytes 8..N   12-byte frame records:
//                  [u32 reserved=0][u16 x][u16 y][u16 w][u16 h]
//   bytes N..M   trailing index table of (u16 frame_id, u16 count) pairs
//                — purpose unclear, likely groups frames into animations.
//                We don't use it.
//   bytes M..end "icons.png\0" + padding
//
// `inv_frame` from each item .cfg is the index into the frame array.
// Frames 0 and 1 are typically header-ish (zero or tiny rects); we surface
// them as null so consumers fall back to the category badge.
public sealed class SpriteAtlas
{
    public string AtlasImagePath { get; }
    public IReadOnlyList<SpriteFrame?> Frames { get; }

    public SpriteAtlas(string atlasImagePath, IReadOnlyList<SpriteFrame?> frames)
    {
        AtlasImagePath = atlasImagePath;
        Frames = frames;
    }

    public SpriteFrame? Lookup(int frameIndex) =>
        frameIndex >= 0 && frameIndex < Frames.Count ? Frames[frameIndex] : null;

    public static SpriteAtlas? LoadFromGameInstall(string gameInstallDir)
    {
        if (string.IsNullOrEmpty(gameInstallDir)) return null;
        var spritesDir = Path.Combine(gameInstallDir, "data", "sprites");
        var bsprPath = Path.Combine(spritesDir, "icons.bspr");
        var atlasPath = Path.Combine(spritesDir, "icons.png");
        if (!File.Exists(bsprPath) || !File.Exists(atlasPath)) return null;

        try
        {
            var bytes = File.ReadAllBytes(bsprPath);
            var frames = ParseBspr(bytes);
            if (frames.Count == 0) return null;
            return new SpriteAtlas(atlasPath, frames);
        }
        catch
        {
            return null;
        }
    }

    private static List<SpriteFrame?> ParseBspr(byte[] bytes)
    {
        // Sanity: must start with "BSPR".
        if (bytes.Length < 12 || bytes[0] != (byte)'B' || bytes[1] != (byte)'S'
            || bytes[2] != (byte)'P' || bytes[3] != (byte)'R')
            return new List<SpriteFrame?>();

        // The trailing index table starts where consecutive 4-byte tuples
        // (u16, u16) look like (small-id, small-count) pairs. Walk forward
        // looking for the boundary; don't try to interpret index entries.
        int dataEnd = FindTrailerStart(bytes);

        var frames = new List<SpriteFrame?>();
        for (int off = 8; off + 12 <= dataEnd; off += 12)
        {
            uint reserved = (uint)(bytes[off] | (bytes[off + 1] << 8)
                                  | (bytes[off + 2] << 16) | (bytes[off + 3] << 24));
            int x = bytes[off + 4] | (bytes[off + 5] << 8);
            int y = bytes[off + 6] | (bytes[off + 7] << 8);
            int w = bytes[off + 8] | (bytes[off + 9] << 8);
            int h = bytes[off + 10] | (bytes[off + 11] << 8);

            // Header-ish or padding records typically have non-zero reserved
            // or zero dimensions — skip those, the consumer falls back to the
            // category badge.
            if (reserved == 0 && w > 0 && h > 0 && w <= 2048 && h <= 2048)
                frames.Add(new SpriteFrame(x, y, w, h));
            else
                frames.Add(null);
        }
        return frames;
    }

    // Locate where the rect-record area transitions to the trailing index
    // table by scanning forward for a stretch of plausible (u16 small,
    // u16 1..8) tuples. Falls back to the "icons.png" string offset minus
    // any padding when the heuristic fails.
    private static int FindTrailerStart(byte[] bytes)
    {
        var atlasNameOffset = FindAsciiOffset(bytes, "icons.png");
        int dataEnd = atlasNameOffset > 0 ? atlasNameOffset : bytes.Length;

        // Scan for the first 4-byte tuple that looks like an index entry,
        // followed by enough similar tuples to be the table.
        for (int off = 8; off + 16 <= dataEnd; off += 4)
        {
            int a = bytes[off] | (bytes[off + 1] << 8);
            int b = bytes[off + 2] | (bytes[off + 3] << 8);
            if (a >= 2000 || b < 1 || b > 8) continue;

            bool consistent = true;
            for (int j = off; j + 4 <= dataEnd; j += 4)
            {
                int aa = bytes[j] | (bytes[j + 1] << 8);
                int bb = bytes[j + 2] | (bytes[j + 3] << 8);
                if (aa >= 2000 || bb < 1 || bb > 8) { consistent = false; break; }
            }
            if (consistent) return off;
        }

        return dataEnd;
    }

    private static int FindAsciiOffset(byte[] bytes, string s)
    {
        var needle = System.Text.Encoding.ASCII.GetBytes(s);
        for (int i = 0; i + needle.Length <= bytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
                if (bytes[i + j] != needle[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }
}
