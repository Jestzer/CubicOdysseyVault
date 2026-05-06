using CommunityToolkit.Mvvm.ComponentModel;
using CubicOdysseyVault.Core.Saves;

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

    public SaveAccountViewModel(SaveAccount account)
    {
        Account = account;
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
