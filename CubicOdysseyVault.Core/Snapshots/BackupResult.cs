namespace CubicOdysseyVault.Core.Snapshots;

public sealed class BackupResult
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public Snapshot? Snapshot { get; set; }
    public string? Reason { get; set; }
}
