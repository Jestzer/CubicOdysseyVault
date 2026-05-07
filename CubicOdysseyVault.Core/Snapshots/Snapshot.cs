using CubicOdysseyVault.Core.Integrity;

namespace CubicOdysseyVault.Core.Snapshots;

public sealed class Snapshot
{
    public string Id { get; set; } = "";
    public DateTime CapturedAtUtc { get; set; }
    public SnapshotTrigger Trigger { get; set; }
    public string? Tag { get; set; }
    public string CombinedHash { get; set; } = "";
    public Dictionary<string, string> FileHashes { get; set; } = new();
    public long TotalBytes { get; set; }
    public SlotHealth Health { get; set; }
    public string SourceKind { get; set; } = "";
    public string FolderName { get; set; } = "";
}

public sealed class SnapshotManifest
{
    public int SchemaVersion { get; set; } = 1;
    public List<Snapshot> Snapshots { get; set; } = new();
}
