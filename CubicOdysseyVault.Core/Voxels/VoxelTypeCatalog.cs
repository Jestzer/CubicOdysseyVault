namespace CubicOdysseyVault.Core.Voxels;

// Maps the high byte of a vw3 / binvox-3 block-info uint32 to a voxel
// definition (identifier + authentic in-game color), so the map renderers
// can paint each block with its real color instead of a hash-derived one.
//
// Mapping was reverse-engineered against pristine save chunks: line N
// (1-based) of <install>/data/configs/voxels/voxels.lst corresponds to
// high-byte 0x0N. (Cross-checks: 0x5D=93 → "alien_grass6", 0x4D=77 →
// "dirt_temperate_base", 0xD3=211 → "wall_wood" — all plausible terrain /
// build matches in the saves we have.)
//
// Loaded alongside ItemCatalog from the game install dir; falls back to
// VoxelTypeCatalog.Empty when the install isn't found, in which case the
// renderers keep using their hash-derived palette.
public sealed record VoxelDefinition(
    string Identifier,
    byte R, byte G, byte B,
    string? TopTexture = null,
    string? SideTexture = null);

public sealed class VoxelTypeCatalog
{
    public IReadOnlyDictionary<byte, VoxelDefinition> ByHighByte { get; }
    public string? GameInstallDir { get; }
    public bool IsEmpty => ByHighByte.Count == 0;

    public VoxelTypeCatalog(
        IReadOnlyDictionary<byte, VoxelDefinition> byHighByte,
        string? gameInstallDir = null)
    {
        ByHighByte = byHighByte;
        GameInstallDir = gameInstallDir;
    }

    public static VoxelTypeCatalog Empty { get; } =
        new(new Dictionary<byte, VoxelDefinition>());

    public VoxelDefinition? Lookup(byte highByte) =>
        ByHighByte.TryGetValue(highByte, out var v) ? v : null;

    public static VoxelTypeCatalog LoadFrom(string gameInstallDir)
    {
        if (string.IsNullOrEmpty(gameInstallDir)) return Empty;
        var voxelsDir = Path.Combine(gameInstallDir, "data", "configs", "voxels");
        var listPath = Path.Combine(voxelsDir, "voxels.lst");
        if (!File.Exists(listPath)) return Empty;

        string[] lines;
        try { lines = File.ReadAllLines(listPath); }
        catch { return Empty; }

        var dict = new Dictionary<byte, VoxelDefinition>();
        // Index 0 of the file → high-byte 0x01. The high byte fits in a
        // single byte so we cap at 255 entries; voxels.lst has 290 in the
        // current build, but only the first 255 are addressable as a high
        // byte and the trailing entries weren't observed in any save we
        // looked at.
        int max = Math.Min(lines.Length, 255);
        for (int i = 0; i < max; i++)
        {
            var name = lines[i].Trim();
            if (name.Length == 0) continue;
            var cfgPath = Path.Combine(voxelsDir, name + ".cfg");
            // Filenames on the lst use mixed case ("Leaves_1") but the cfg
            // files on disk may use a different case. On case-sensitive
            // filesystems we resolve by directory scan when the literal
            // path doesn't exist.
            if (!File.Exists(cfgPath))
            {
                cfgPath = ResolveCaseInsensitive(voxelsDir, name + ".cfg") ?? cfgPath;
                if (!File.Exists(cfgPath)) continue;
            }
            var parsed = VoxelConfigParser.Parse(cfgPath);
            if (parsed.Color is null) continue;
            // High byte indexing is 1-based: voxels.lst line 1 → 0x01.
            byte highByte = (byte)(i + 1);
            dict[highByte] = new VoxelDefinition(
                name,
                parsed.Color.Value.R, parsed.Color.Value.G, parsed.Color.Value.B,
                TopTexture: parsed.DefaultTextureTop,
                SideTexture: parsed.DefaultTexture);
        }

        return new VoxelTypeCatalog(dict, gameInstallDir);
    }

    public static VoxelTypeCatalog AutoDiscover(IEnumerable<string> candidateInstallDirs)
    {
        foreach (var dir in candidateInstallDirs)
        {
            var catalog = LoadFrom(dir);
            if (!catalog.IsEmpty) return catalog;
        }
        return Empty;
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
