using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class SaveAccountViewModel : ViewModelBase
{
    public SaveAccount Account { get; }
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
    [NotifyPropertyChangedFor(nameof(LastSnapshotText), nameof(SnapshotCount))]
    private ObservableCollection<SnapshotViewModel> _snapshots = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackUpNowCommand))]
    private bool _isBackingUp;

    [ObservableProperty] private string? _backupStatus;

    public Func<SaveAccount, SnapshotTrigger, Task<BackupResult>>? BackupRequested { get; set; }

    public string LastSnapshotText => Snapshots.Count == 0
        ? "Never backed up"
        : $"Last backup: {Snapshots[0].CapturedAtText}";

    public int SnapshotCount => Snapshots.Count;

    public SaveAccountViewModel(SaveAccount account)
    {
        Account = account;
    }

    public void SetSnapshots(IEnumerable<Snapshot> snapshots)
    {
        Snapshots.Clear();
        foreach (var s in snapshots.OrderByDescending(s => s.CapturedAtUtc))
            Snapshots.Add(new SnapshotViewModel(s));
        OnPropertyChanged(nameof(LastSnapshotText));
        OnPropertyChanged(nameof(SnapshotCount));
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
                    Snapshots.Insert(0, new SnapshotViewModel(result.Snapshot));
                    OnPropertyChanged(nameof(LastSnapshotText));
                    OnPropertyChanged(nameof(SnapshotCount));
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

    private bool CanBackUp() => !IsBackingUp;

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
