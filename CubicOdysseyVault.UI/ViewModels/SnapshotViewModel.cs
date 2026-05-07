using CommunityToolkit.Mvvm.ComponentModel;
using CubicOdysseyVault.Core.Snapshots;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class SnapshotViewModel : ViewModelBase
{
    public Snapshot Snapshot { get; }
    public string Id => Snapshot.Id;
    public string CapturedAtText => Snapshot.CapturedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string TriggerLabel => Snapshot.Trigger.ToString();
    public string? Tag => Snapshot.Tag;
    public string FormattedSize => FormatBytes(Snapshot.TotalBytes);
    public string HealthLabel => Snapshot.Health.ToString();
    public int FileCount => Snapshot.FileHashes.Count;

    public SnapshotViewModel(Snapshot snapshot)
    {
        Snapshot = snapshot;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double v = bytes;
        string[] units = { "KB", "MB", "GB", "TB" };
        int i = -1;
        do { v /= 1024; i++; } while (v >= 1024 && i < units.Length - 1);
        return $"{v:0.##} {units[i]}";
    }
}
