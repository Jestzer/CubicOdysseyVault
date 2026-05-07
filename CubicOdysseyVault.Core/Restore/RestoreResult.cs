using CubicOdysseyVault.Core.Snapshots;

namespace CubicOdysseyVault.Core.Restore;

public sealed class RestoreResult
{
    public bool Success { get; set; }
    public bool BlockedByRunningGame { get; set; }
    public string? Reason { get; set; }
    public Snapshot? PreRestoreSnapshot { get; set; }
    public string? ReplacedFolderPath { get; set; }
}
