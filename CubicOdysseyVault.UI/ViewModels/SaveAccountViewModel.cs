using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core.Integrity;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class SaveAccountViewModel : ViewModelBase
{
    public SaveAccount Account { get; }
    public string SteamId32 => Account.SteamId32;
    public string SourceLabel => Account.Source.Kind switch
    {
        SaveSourceKind.ProtonCompatdata => "Proton compatdata",
        SaveSourceKind.SteamCloudRemote => "Steam Cloud",
        SaveSourceKind.Documents => "Documents",
        SaveSourceKind.Manual => "Manual override",
        _ => Account.Source.Kind.ToString(),
    };
    public string FormattedSize => FormatBytes(Account.TotalBytes);
    public string LastWriteText => Account.LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public int FileCount => Account.AccountFiles.Count;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(LastSnapshotText), nameof(SnapshotCount), nameof(LatestHealth), nameof(LatestHealthLabel),
        nameof(IsHealthHealthy), nameof(IsHealthSuspicious), nameof(IsHealthCorrupted), nameof(IsHealthUnchecked))]
    private ObservableCollection<SnapshotViewModel> _snapshots = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackUpNowCommand))]
    private bool _isBackingUp;

    [ObservableProperty] private string? _backupStatus;

    public Func<SaveAccount, SnapshotTrigger, Task<BackupResult>>? BackupRequested { get; set; }
    public Func<SaveAccount, Snapshot, Task>? OnInspectSnapshotRequested { get; set; }
    public Func<SaveAccount, Snapshot, Task>? OnRestoreRequested { get; set; }
    public Func<SaveAccount, Snapshot, Task>? OnEditTagRequested { get; set; }
    public Func<SaveAccount, Snapshot, Task>? OnDeleteRequested { get; set; }

    public string LastSnapshotText => Snapshots.Count == 0
        ? "Never backed up"
        : $"Last backup: {Snapshots[0].CapturedAtText}";

    public int SnapshotCount => Snapshots.Count;

    public SlotHealth? LatestHealth => Snapshots.Count == 0 ? null : Snapshots[0].Snapshot.Health;

    public string LatestHealthLabel => LatestHealth switch
    {
        SlotHealth.Healthy => "Healthy",
        SlotHealth.Suspicious => "Suspicious",
        SlotHealth.Corrupted => "Corrupted",
        _ => "Unchecked",
    };

    public bool IsHealthHealthy => LatestHealth == SlotHealth.Healthy;
    public bool IsHealthSuspicious => LatestHealth == SlotHealth.Suspicious;
    public bool IsHealthCorrupted => LatestHealth == SlotHealth.Corrupted;
    public bool IsHealthUnchecked => LatestHealth == null;

    public bool IsOrphan { get; }

    public SaveAccountViewModel(SaveAccount account, bool isOrphan = false)
    {
        Account = account;
        IsOrphan = isOrphan;
    }

    public void SetSnapshots(IEnumerable<Snapshot> snapshots)
    {
        Snapshots.Clear();
        foreach (var s in snapshots.OrderByDescending(s => s.CapturedAtUtc))
            Snapshots.Add(WireSnapshot(new SnapshotViewModel(s)));
        OnPropertyChanged(nameof(LastSnapshotText));
        OnPropertyChanged(nameof(SnapshotCount));
        OnPropertyChanged(nameof(LatestHealth));
        OnPropertyChanged(nameof(LatestHealthLabel));
        OnPropertyChanged(nameof(IsHealthHealthy));
        OnPropertyChanged(nameof(IsHealthSuspicious));
        OnPropertyChanged(nameof(IsHealthCorrupted));
        OnPropertyChanged(nameof(IsHealthUnchecked));
    }

    private SnapshotViewModel WireSnapshot(SnapshotViewModel svm)
    {
        svm.OnInspectRequested = snap =>
            OnInspectSnapshotRequested?.Invoke(Account, snap) ?? Task.CompletedTask;
        svm.OnRestoreRequested = snap =>
            OnRestoreRequested?.Invoke(Account, snap) ?? Task.CompletedTask;
        svm.OnEditTagRequested = snap =>
            OnEditTagRequested?.Invoke(Account, snap) ?? Task.CompletedTask;
        svm.OnDeleteRequested = snap =>
            OnDeleteRequested?.Invoke(Account, snap) ?? Task.CompletedTask;
        return svm;
    }

    [RelayCommand(CanExecute = nameof(CanBackUp))]
    private Task BackUpNow() => BackUpAsync(SnapshotTrigger.Manual);

    public async Task BackUpAsync(SnapshotTrigger trigger)
    {
        if (BackupRequested == null || IsBackingUp) return;
        IsBackingUp = true;
        BackupStatus = trigger == SnapshotTrigger.Auto ? "Auto-backing up..." : "Backing up...";
        try
        {
            var result = await BackupRequested(Account, trigger);
            if (result.Success)
            {
                if (result.Skipped)
                {
                    BackupStatus = "No changes since last snapshot.";
                }
                else if (result.Snapshot != null)
                {
                    Snapshots.Insert(0, WireSnapshot(new SnapshotViewModel(result.Snapshot)));
                    OnPropertyChanged(nameof(LastSnapshotText));
                    OnPropertyChanged(nameof(SnapshotCount));
                    OnPropertyChanged(nameof(LatestHealth));
                    OnPropertyChanged(nameof(LatestHealthLabel));
                    OnPropertyChanged(nameof(IsHealthHealthy));
                    OnPropertyChanged(nameof(IsHealthSuspicious));
                    OnPropertyChanged(nameof(IsHealthCorrupted));
                    OnPropertyChanged(nameof(IsHealthUnchecked));
                    BackupStatus = (trigger == SnapshotTrigger.Auto ? "Auto-saved" : "Saved")
                        + $" at {result.Snapshot.CapturedAtUtc.ToLocalTime():HH:mm:ss}";
                }
            }
            else
            {
                BackupStatus = result.Reason ?? "Backup failed.";
            }
        }
        catch (Exception ex)
        {
            BackupStatus = $"Backup failed: {ex.Message}";
        }
        finally
        {
            IsBackingUp = false;
        }
    }

    private bool CanBackUp() => !IsBackingUp && !IsOrphan;

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
