using CommunityToolkit.Mvvm.ComponentModel;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private string _statusMessage =
        "Skeleton ready. Discovery, snapshots, and restore land in upcoming phases.";
}
