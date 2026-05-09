namespace CubicOdysseyVault.Core.SaveContent;

public sealed record SpriteFrame(int X, int Y, int Width, int Height);

// Cubic Odyssey ships its inventory-item icons in items01.png at
// <install>/data/sprites/, with rectangles described in items01.bspr.
// (icons.png is the much smaller UI/HUD atlas — wrong file for item icons.)
// BSPR layout:
//
//   bytes 0..3   "BSPR"
//   bytes 4..7   header u32
//   bytes 8..N   12-byte frame records:
//                  [u32 reserved=0][u16 x][u16 y][u16 w][u16 h]
//   bytes N..M   trailing index table of (u16 frame_id, u16 count) pairs
//                — likely animation groupings. We don't use it: each item
//                .cfg's inv_frame indexes directly into the frame array.
//   bytes M..end "items01.png\0" + padding
//
// items01 records form a regular 160x160 grid covering ~400 inventory items.
// Records with reserved!=0 or zero/oversized dims are header/padding entries
// and are surfaced as null so consumers fall back to the category badge.
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
        var bsprPath = Path.Combine(spritesDir, "items01.bspr");
        var atlasPath = Path.Combine(spritesDir, "items01.png");
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
            // or zero/oversized dimensions (often 0xFFFF sentinels) — skip
            // those, the consumer falls back to the category badge. Real
            // inventory icons are <= 256px per side; the upper bound stays
            // generous to tolerate larger atlases.
            if (reserved == 0 && w > 0 && h > 0 && w <= 4096 && h <= 4096)
                frames.Add(new SpriteFrame(x, y, w, h));
            else
                frames.Add(null);
        }
        return frames;
    }

    // Locate where the rect-record area transitions to the trailing index
    // table by scanning forward for a stretch of plausible (u16 small,
    // u16 1..8) tuples. Falls back to a known atlas-filename ASCII anchor
    // ("items01.png", "icons.png") when the heuristic fails.
    private static int FindTrailerStart(byte[] bytes)
    {
        int atlasNameOffset = FindAsciiOffset(bytes, "items01.png");
        if (atlasNameOffset <= 0) atlasNameOffset = FindAsciiOffset(bytes, "icons.png");
        int dataEnd = atlasNameOffset > 0 ? atlasNameOffset : bytes.Length;

        // Scan for the first 4-byte tuple that looks like an index entry,
        // followed by enough similar tuples to be the table. Accept count==0
        // because items01.bspr ends its trailer with a (fid, 0) sentinel and
        // we don't want one bad tuple to reject the entire trailer.
        for (int off = 8; off + 16 <= dataEnd; off += 4)
        {
            int a = bytes[off] | (bytes[off + 1] << 8);
            int b = bytes[off + 2] | (bytes[off + 3] << 8);
            if (a >= 5000 || b > 16) continue;

            bool consistent = true;
            for (int j = off; j + 4 <= dataEnd; j += 4)
            {
                int aa = bytes[j] | (bytes[j + 1] << 8);
                int bb = bytes[j + 2] | (bytes[j + 3] << 8);
                if (aa >= 5000 || bb > 16) { consistent = false; break; }
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
