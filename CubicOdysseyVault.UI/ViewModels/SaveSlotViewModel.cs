using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CubicOdysseyVault.Core.Saves;

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

    public SaveSlotViewModel(SaveSlot slot)
    {
        Slot = slot;
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
