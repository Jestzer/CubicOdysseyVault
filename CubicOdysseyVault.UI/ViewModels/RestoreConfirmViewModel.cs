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
    public Bitmap? Screenshot { get; }
    public bool HasScreenshot => Screenshot != null;
    public string CapturedAtText { get; }
    public string FormattedSize { get; }
    public string SourceLabel { get; }
    public string TriggerLabel { get; }
    public string? Tag { get; }
    public int FileCount { get; }
    public string SlotHeader { get; }
    public string BodyText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRestore))]
    private bool _isGameRunning;

    public bool CanRestore => !IsGameRunning;
    public bool Confirmed { get; private set; }

    public Action? CloseRequested { get; set; }

    private RestoreConfirmViewModel(
        Snapshot snapshot,
        Bitmap? screenshot,
        string sourceLabel,
        string slotHeader,
        string bodyText)
    {
        Snapshot = snapshot;
        Screenshot = screenshot;
        SourceLabel = sourceLabel;
        SlotHeader = slotHeader;
        BodyText = bodyText;

        CapturedAtText = snapshot.CapturedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        FormattedSize = FormatBytes(snapshot.TotalBytes);
        TriggerLabel = snapshot.Trigger switch
        {
            SnapshotTrigger.Manual => "Manual",
            SnapshotTrigger.Auto => "Auto",
            SnapshotTrigger.PreRestore => "Pre-restore",
            _ => snapshot.Trigger.ToString(),
        };
        Tag = snapshot.Tag;
        FileCount = snapshot.FileHashes.Count;

        IsGameRunning = GameProcessDetector.IsCubicOdysseyRunning();
    }

    public static RestoreConfirmViewModel ForSlot(SaveSlot slot, Snapshot snapshot, string snapshotFolder) =>
        new(
            snapshot,
            screenshot: TgaBitmapLoader.TryLoad(Path.Combine(snapshotFolder, "screenshot.tga")),
            sourceLabel: SourceLabelFor(slot.Source.Kind),
            slotHeader: $"Slot {slot.SlotName} · acct {slot.AccountFolderName} ({slot.SteamId32})",
            bodyText: "The live slot will be replaced with this snapshot's contents. The current state is captured as a Pre-restore snapshot first, so this restore is itself undoable.");

    public static RestoreConfirmViewModel ForAccount(SaveAccount account, Snapshot snapshot, string snapshotFolder) =>
        new(
            snapshot,
            screenshot: null,
            sourceLabel: SourceLabelFor(account.Source.Kind),
            slotHeader: $"Account-level · ({account.SteamId32})",
            bodyText: "The live account-level files will be replaced with this snapshot's contents. Slot subfolders are not affected. The current state is captured as a Pre-restore snapshot first, so this restore is itself undoable.");

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

    private static string SourceLabelFor(SaveSourceKind kind) => kind switch
    {
        SaveSourceKind.ProtonCompatdata => "Proton compatdata",
        SaveSourceKind.SteamCloudRemote => "Steam Cloud",
        SaveSourceKind.Documents => "Documents",
        SaveSourceKind.Manual => "Manual override",
        _ => kind.ToString(),
    };

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
