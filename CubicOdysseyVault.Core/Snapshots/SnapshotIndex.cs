using System.Text.Json;
using System.Text.Json.Serialization;

namespace CubicOdysseyVault.Core.Snapshots;

public static class SnapshotIndex
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static SnapshotManifest Load(string manifestPath)
    {
        try
        {
            if (!File.Exists(manifestPath)) return new SnapshotManifest();
            var json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<SnapshotManifest>(json, Options) ?? new SnapshotManifest();
        }
        catch
        {
            return new SnapshotManifest();
        }
    }

    // Atomic rewrite: write to *.tmp then File.Move(overwrite). The .NET 8 Move is
    // atomic on POSIX and best-effort atomic on Windows (uses replace semantics).
    public static void Save(SnapshotManifest manifest, string manifestPath)
    {
        var dir = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmpPath = manifestPath + ".tmp";
        var json = JsonSerializer.Serialize(manifest, Options);
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, manifestPath, overwrite: true);
    }
}
