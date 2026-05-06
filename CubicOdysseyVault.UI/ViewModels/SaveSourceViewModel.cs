using CommunityToolkit.Mvvm.ComponentModel;
using CubicOdysseyVault.Core.Saves;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class SaveSourceViewModel : ViewModelBase
{
    public SaveSource Source { get; }
    public string DisplayLabel { get; }
    public string PathTooltip => Source.RootPath;
    [ObservableProperty] private bool _exists;

    public SaveSourceViewModel(SaveSource source)
    {
        Source = source;
        Exists = source.Exists;
        DisplayLabel = source.Kind switch
        {
            SaveSourceKind.ProtonCompatdata => "Proton compatdata",
            SaveSourceKind.SteamCloudRemote => "Steam Cloud",
            SaveSourceKind.Documents => "Documents",
            SaveSourceKind.Manual => "Manual override",
            _ => source.Kind.ToString(),
        };
    }
}
