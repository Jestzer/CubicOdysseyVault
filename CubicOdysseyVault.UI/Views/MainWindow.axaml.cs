using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;
using CubicOdysseyVault.UI.Services;
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
        if (DataContext is not MainWindowViewModel vm) return;

        vm.ShowSettingsDialog = ShowSettingsDialogAsync;
        vm.ShowOnboardingDialog = ShowOnboardingDialogAsync;
        vm.ShowRestoreConfirmDialog = ShowRestoreConfirmDialogAsync;
        vm.ShowTagEditDialog = ShowTagEditDialogAsync;
        vm.ShowDeleteConfirmDialog = ShowDeleteConfirmDialogAsync;
        vm.OpenBackupFolderRequested = OpenInFileManager;

        if (!vm.HasDiscovered && !vm.IsDiscovering)
            await vm.RefreshDiscoveryCommand.ExecuteAsync(null);
    }

    private async Task<AppSettings?> ShowSettingsDialogAsync(AppSettings current)
    {
        var vm = new SettingsViewModel(current);
        var dialog = new SettingsDialog { DataContext = vm };
        await dialog.ShowDialog(this);
        return vm.WasSaved ? vm.ApplyTo(current) : null;
    }

    private async Task<AppSettings?> ShowOnboardingDialogAsync(AppSettings current, int users, int slots, int sources)
    {
        var vm = new OnboardingViewModel(current, users, slots, sources);
        var dialog = new OnboardingDialog { DataContext = vm };
        await dialog.ShowDialog(this);
        return vm.WasCompleted ? vm.ApplyTo(current) : null;
    }

    private async Task<bool> ShowRestoreConfirmDialogAsync(SaveSlot slot, Snapshot snapshot, string snapshotFolder)
    {
        var vm = new RestoreConfirmViewModel(slot, snapshot, snapshotFolder);
        var dialog = new RestoreConfirmDialog { DataContext = vm };
        await dialog.ShowDialog(this);
        return vm.Confirmed;
    }

    private async Task<string?> ShowTagEditDialogAsync(string label, string? currentTag)
    {
        var vm = new TagEditViewModel(label, currentTag);
        var dialog = new TagEditDialog { DataContext = vm };
        await dialog.ShowDialog(this);
        return vm.Confirmed ? vm.Result : null;
    }

    private async Task<bool> ShowDeleteConfirmDialogAsync(Snapshot snapshot)
    {
        var vm = new DeleteConfirmViewModel(snapshot);
        var dialog = new DeleteConfirmDialog { DataContext = vm };
        await dialog.ShowDialog(this);
        return vm.Confirmed;
    }

    private static void OpenInFileManager(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start(new ProcessStartInfo("open", $"\"{path}\"") { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{path}\"") { UseShellExecute = true });
        }
        catch
        {
            // Best effort — if no file manager is available, silently fail.
        }
    }
}
