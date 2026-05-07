using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core.Restore;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;
using CubicOdysseyVault.UI.Services;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class RestoreConfirmViewModel : ViewModelBase
{
    public Snapshot Snapshot { get; }
    public SaveSlot Slot { get; }
    public Bitmap? Screenshot { get; }
    public string CapturedAtText { get; }
    public string FormattedSize { get; }
    public string SourceLabel { get; }
    public string TriggerLabel { get; }
    public string? Tag { get; }
    public int FileCount { get; }
    public string SlotHeader { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRestore))]
    private bool _isGameRunning;

    public bool CanRestore => !IsGameRunning;
    public bool Confirmed { get; private set; }

    public Action? CloseRequested { get; set; }

    public RestoreConfirmViewModel(SaveSlot slot, Snapshot snapshot, string snapshotFolder)
    {
        Slot = slot;
        Snapshot = snapshot;

        Screenshot = TgaBitmapLoader.TryLoad(Path.Combine(snapshotFolder, "screenshot.tga"));
        CapturedAtText = snapshot.CapturedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        FormattedSize = FormatBytes(snapshot.TotalBytes);
        SourceLabel = slot.Source.Kind switch
        {
            SaveSourceKind.ProtonCompatdata => "Proton compatdata",
            SaveSourceKind.SteamCloudRemote => "Steam Cloud",
            SaveSourceKind.Documents => "Documents",
            SaveSourceKind.Manual => "Manual override",
            _ => slot.Source.Kind.ToString(),
        };
        TriggerLabel = snapshot.Trigger switch
        {
            SnapshotTrigger.Manual => "Manual",
            SnapshotTrigger.Auto => "Auto",
            SnapshotTrigger.PreRestore => "Pre-restore",
            _ => snapshot.Trigger.ToString(),
        };
        Tag = snapshot.Tag;
        FileCount = snapshot.FileHashes.Count;
        SlotHeader = $"Slot {slot.SlotName} / acct {slot.AccountFolderName} ({slot.SteamId32})";

        IsGameRunning = GameProcessDetector.IsCubicOdysseyRunning();
    }

    [RelayCommand]
    private void RecheckGame() => IsGameRunning = GameProcessDetector.IsCubicOdysseyRunning();

    [RelayCommand]
    private void ConfirmRestore()
    {
        if (!CanRestore) return;
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
