using Avalonia.Controls;
using CubicOdysseyVault.UI.ViewModels;

namespace CubicOdysseyVault.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && !vm.HasDiscovered && !vm.IsDiscovering)
            await vm.RefreshDiscoveryCommand.ExecuteAsync(null);
    }
}
