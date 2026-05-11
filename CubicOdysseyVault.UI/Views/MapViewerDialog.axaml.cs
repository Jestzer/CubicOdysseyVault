using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

        // Wheel-to-zoom on the render pane. Routed (Tunnel) so we see it
        // before the ScrollViewer's own wheel handling consumes it.
        AddHandler(PointerWheelChangedEvent, OnPointerWheelTunnel,
            RoutingStrategies.Tunnel);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MapViewerViewModel vm) return;
        vm.CloseRequested = () => Close();
    }

    private void OnPointerWheelTunnel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MapViewerViewModel vm) return;
        // Only intercept wheel when the pointer is over the render pane —
        // the ScrollViewer named RenderScroll. Anywhere else (chunk list,
        // side panel) keeps native wheel behavior.
        var scroll = this.FindControl<ScrollViewer>("RenderScroll");
        if (scroll is null || !scroll.IsPointerOver) return;
        if (e.Delta.Y > 0) vm.ZoomInCommand.Execute(null);
        else if (e.Delta.Y < 0) vm.ZoomOutCommand.Execute(null);
        e.Handled = true;
    }
}
