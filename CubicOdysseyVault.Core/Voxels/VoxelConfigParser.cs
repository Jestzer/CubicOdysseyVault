using System.Globalization;

namespace CubicOdysseyVault.Core.Voxels;

// Pulls m_color and texture references out of a Cubic Odyssey voxel cfg
// file. Each voxel definition lives at <install>/data/configs/voxels/
// <name>.cfg as a single VoxelCfg block, e.g.:
//   VoxelCfg
//   {
//       name                  "Wall Metal 7"
//       m_defaultTexture      "wall_metal_7_side"
//       m_defaultTextureTop   "wall_metal_7"
//       m_color               [146,148,151,255]
//       ...
//   }
// We only care about m_color and the texture names here — the rest of the
// metadata is ignored. m_color accepts both [r,g,b] and [r,g,b,a]; alpha
// is dropped because the renderer composes its own.
public static class VoxelConfigParser
{
    public readonly record struct Rgb(byte R, byte G, byte B);

    public sealed record Parsed(
        Rgb? Color,
        string? DefaultTexture,
        string? DefaultTextureTop);

    public static Rgb? ParseColor(string path)
    {
        try { return ParseColorText(File.ReadAllText(path)); }
        catch { return null; }
    }

    public static Rgb? ParseColorText(string text) => ParseText(text).Color;

    public static Parsed Parse(string path)
    {
        try { return ParseText(File.ReadAllText(path)); }
        catch { return new Parsed(null, null, null); }
    }

    public static Parsed ParseText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new Parsed(null, null, null);
        return new Parsed(
            Color: ExtractColor(text),
            DefaultTexture: ExtractQuoted(text, "m_defaultTexture"),
            DefaultTextureTop: ExtractQuoted(text, "m_defaultTextureTop"));
    }

    private static Rgb? ExtractColor(string text)
    {
        int idx = text.IndexOf("m_color", StringComparison.Ordinal);
        if (idx < 0) return null;

        int open = text.IndexOf('[', idx);
        int close = open < 0 ? -1 : text.IndexOf(']', open);
        if (open < 0 || close < 0 || close <= open + 1) return null;

        var parts = text[(open + 1)..close].Split(',');
        if (parts.Length < 3) return null;
        if (!byte.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
            return null;
        return new Rgb(r, g, b);
    }

    // Pulls the value of `key  "value"` out of the cfg, ignoring whitespace.
    // Disambiguates "m_defaultTexture" from "m_defaultTextureTop" by
    // requiring the key to be followed by whitespace before the quote.
    private static string? ExtractQuoted(string text, string key)
    {
        int searchFrom = 0;
        while (searchFrom < text.Length)
        {
            int idx = text.IndexOf(key, searchFrom, StringComparison.Ordinal);
            if (idx < 0) return null;
            int after = idx + key.Length;
            // Reject prefix matches: the next char must be whitespace.
            if (after >= text.Length || !char.IsWhiteSpace(text[after]))
            {
                searchFrom = idx + 1;
                continue;
            }
            int open = text.IndexOf('"', after);
            if (open < 0) return null;
            int close = text.IndexOf('"', open + 1);
            if (close < 0) return null;
            return text[(open + 1)..close];
        }
        return null;
    }
}
