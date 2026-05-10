using System;
using Avalonia.Controls;
using CubicOdysseyVault.UI.ViewModels;

namespace CubicOdysseyVault.UI.Views;

public partial class MapViewerDialog : Window
{
    public MapViewerDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += (_, _) =>
        {
            if (DataContext is MapViewerViewModel vm)
                vm.Load();
        };
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MapViewerViewModel vm) return;
        vm.CloseRequested = () => Close();
    }
}
