using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class SaveSlotViewModel : ViewModelBase
{
    public SaveSlot Slot { get; }
    public string SlotName => Slot.SlotName;
    public string AccountFolderName => Slot.AccountFolderName;
    public string SourceLabel => Slot.Source.Kind switch
    {
        SaveSourceKind.ProtonCompatdata => "Proton compatdata",
        SaveSourceKind.SteamCloudRemote => "Steam Cloud",
        SaveSourceKind.Documents => "Documents",
        SaveSourceKind.Manual => "Manual override",
        _ => Slot.Source.Kind.ToString(),
    };
    public string FormattedSize => FormatBytes(Slot.TotalBytes);
    public string LastWriteText => Slot.LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public int FileCount => Slot.Files.Count;
    public bool HasScreenshot => Slot.HasScreenshot;

    public string? ScreenshotPath => Slot.Files
        .FirstOrDefault(f => string.Equals(f.FileName, "screenshot.tga", StringComparison.OrdinalIgnoreCase))
        ?.FullPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastSnapshotText), nameof(SnapshotCount))]
    private ObservableCollection<SnapshotViewModel> _snapshots = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackUpNowCommand))]
    private bool _isBackingUp;

    [ObservableProperty] private string? _backupStatus;

    public Func<SaveSlot, Task<BackupResult>>? BackupRequested { get; set; }

    public string LastSnapshotText => Snapshots.Count == 0
        ? "Never backed up"
        : $"Last backup: {Snapshots[0].CapturedAtText}";

    public int SnapshotCount => Snapshots.Count;

    public SaveSlotViewModel(SaveSlot slot)
    {
        Slot = slot;
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
    private async Task BackUpNow()
    {
        if (BackupRequested == null) return;
        IsBackingUp = true;
        BackupStatus = "Backing up...";
        try
        {
            var result = await BackupRequested(Slot);
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
                    BackupStatus = $"Saved at {result.Snapshot.CapturedAtUtc.ToLocalTime():HH:mm:ss}";
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
