using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core.Integrity;
using CubicOdysseyVault.Core.Snapshots;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class SnapshotViewModel : ViewModelBase
{
    public Snapshot Snapshot { get; }
    public string Id => Snapshot.Id;
    public string CapturedAtText => Snapshot.CapturedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string TriggerLabel => Snapshot.Trigger switch
    {
        SnapshotTrigger.Manual => "Manual",
        SnapshotTrigger.Auto => "Auto",
        SnapshotTrigger.PreRestore => "Pre-restore",
        _ => Snapshot.Trigger.ToString(),
    };
    public bool IsTriggerManual => Snapshot.Trigger == SnapshotTrigger.Manual;
    public bool IsTriggerAuto => Snapshot.Trigger == SnapshotTrigger.Auto;
    public bool IsTriggerPreRestore => Snapshot.Trigger == SnapshotTrigger.PreRestore;
    public bool IsHealthHealthy => Snapshot.Health == SlotHealth.Healthy;
    public bool IsHealthSuspicious => Snapshot.Health == SlotHealth.Suspicious;
    public bool IsHealthCorrupted => Snapshot.Health == SlotHealth.Corrupted;
    public string? Tag => Snapshot.Tag;
    public string FormattedSize => FormatBytes(Snapshot.TotalBytes);
    public string HealthLabel => Snapshot.Health.ToString();
    public int FileCount => Snapshot.FileHashes.Count;

    public Func<Snapshot, Task>? OnRestoreRequested { get; set; }
    public Func<Snapshot, Task>? OnEditTagRequested { get; set; }
    public Func<Snapshot, Task>? OnDeleteRequested { get; set; }

    public SnapshotViewModel(Snapshot snapshot)
    {
        Snapshot = snapshot;
    }

    [RelayCommand]
    private async Task Restore()
    {
        if (OnRestoreRequested != null) await OnRestoreRequested(Snapshot);
    }

    [RelayCommand]
    private async Task EditTag()
    {
        if (OnEditTagRequested != null) await OnEditTagRequested(Snapshot);
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (OnDeleteRequested != null) await OnDeleteRequested(Snapshot);
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
