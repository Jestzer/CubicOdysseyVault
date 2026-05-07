using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core.Integrity;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;
using CubicOdysseyVault.UI.Services;

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

    public Bitmap? Screenshot { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(LastSnapshotText), nameof(SnapshotCount), nameof(LatestHealth), nameof(LatestHealthLabel),
        nameof(IsHealthHealthy), nameof(IsHealthSuspicious), nameof(IsHealthCorrupted), nameof(IsHealthUnchecked))]
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

    public SaveSlotViewModel(SaveSlot slot)
    {
        Slot = slot;
        Screenshot = TgaBitmapLoader.TryLoad(ScreenshotPath);
    }

    public void SetSnapshots(IEnumerable<Snapshot> snapshots)
    {
        Snapshots.Clear();
        foreach (var s in snapshots.OrderByDescending(s => s.CapturedAtUtc))
            Snapshots.Add(new SnapshotViewModel(s));
        OnPropertyChanged(nameof(LastSnapshotText));
        OnPropertyChanged(nameof(SnapshotCount));
        OnPropertyChanged(nameof(LatestHealth));
        OnPropertyChanged(nameof(LatestHealthLabel));
        OnPropertyChanged(nameof(IsHealthHealthy));
        OnPropertyChanged(nameof(IsHealthSuspicious));
        OnPropertyChanged(nameof(IsHealthCorrupted));
        OnPropertyChanged(nameof(IsHealthUnchecked));
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
                    OnPropertyChanged(nameof(LatestHealth));
                    OnPropertyChanged(nameof(LatestHealthLabel));
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
