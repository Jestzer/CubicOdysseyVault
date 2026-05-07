using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core.Snapshots;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class DeleteConfirmViewModel : ViewModelBase
{
    public Snapshot Snapshot { get; }
    public string CapturedAtText { get; }
    public string TriggerLabel { get; }
    public string? Tag { get; }
    public string FormattedSize { get; }
    public bool IsTagged { get; }
    public bool IsPreRestore { get; }

    public bool Confirmed { get; private set; }
    public Action? CloseRequested { get; set; }

    public DeleteConfirmViewModel(Snapshot snapshot)
    {
        Snapshot = snapshot;
        CapturedAtText = snapshot.CapturedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        TriggerLabel = snapshot.Trigger switch
        {
            SnapshotTrigger.Manual => "Manual",
            SnapshotTrigger.Auto => "Auto",
            SnapshotTrigger.PreRestore => "Pre-restore",
            _ => snapshot.Trigger.ToString(),
        };
        Tag = snapshot.Tag;
        FormattedSize = FormatBytes(snapshot.TotalBytes);
        IsTagged = !string.IsNullOrEmpty(snapshot.Tag);
        IsPreRestore = snapshot.Trigger == SnapshotTrigger.PreRestore;
    }

    [RelayCommand]
    private void Confirm()
    {
        Confirmed = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        CloseRequested?.Invoke();
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
