using Avalonia.Controls;
using CubicOdysseyVault.UI.ViewModels;

namespace CubicOdysseyVault.UI.Views;

public partial class SaveInspectorDialog : Window
{
    public SaveInspectorDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not SaveInspectorViewModel vm) return;
        vm.CloseRequested = () => Close();
    }
}
