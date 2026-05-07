using Avalonia.Controls;
using CubicOdysseyVault.UI.ViewModels;

namespace CubicOdysseyVault.UI.Views;

public partial class RestoreConfirmDialog : Window
{
    public RestoreConfirmDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not RestoreConfirmViewModel vm) return;
        vm.CloseRequested = () => Close();
    }
}
