using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.UI.Services;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private string _backupRootPath = "";
    [ObservableProperty] private ObservableCollection<string> _manualSourceRoots = new();
    [ObservableProperty] private string? _selectedManualRoot;
    [ObservableProperty] private bool _watcherEnabled = true;
    [ObservableProperty] private int _hourlySnapshotsKept = 24;
    [ObservableProperty] private int _dailySnapshotsKept = 14;
    [ObservableProperty] private int _weeklySnapshotsKept = 8;
    [ObservableProperty] private int _watcherDebounceSeconds = 10;
    [ObservableProperty] private string _gameInstallPath = "";

    public bool WasSaved { get; private set; }

    public Func<Task<string?>>? PickFolderRequested { get; set; }
    public Action? CloseRequested { get; set; }

    public SettingsViewModel(AppSettings settings)
    {
        BackupRootPath = settings.BackupRootPath;
        ManualSourceRoots = new ObservableCollection<string>(settings.ManualSourceRoots);
        WatcherEnabled = settings.WatcherEnabled;
        HourlySnapshotsKept = settings.HourlySnapshotsKept;
        DailySnapshotsKept = settings.DailySnapshotsKept;
        WeeklySnapshotsKept = settings.WeeklySnapshotsKept;
        WatcherDebounceSeconds = settings.WatcherDebounceSeconds;
        GameInstallPath = settings.GameInstallPath;
    }

    [RelayCommand]
    private async Task BrowseBackupRoot()
    {
        if (PickFolderRequested == null) return;
        var path = await PickFolderRequested();
        if (!string.IsNullOrEmpty(path)) BackupRootPath = path;
    }

    [RelayCommand]
    private async Task BrowseGameInstall()
    {
        if (PickFolderRequested == null) return;
        var path = await PickFolderRequested();
        if (!string.IsNullOrEmpty(path)) GameInstallPath = path;
    }

    [RelayCommand]
    private async Task AddManualRoot()
    {
        if (PickFolderRequested == null) return;
        var path = await PickFolderRequested();
        if (!string.IsNullOrEmpty(path) && !ManualSourceRoots.Contains(path))
            ManualSourceRoots.Add(path);
    }

    [RelayCommand]
    private void RemoveManualRoot()
    {
        if (SelectedManualRoot != null)
            ManualSourceRoots.Remove(SelectedManualRoot);
    }

    [RelayCommand]
    private void Save()
    {
        WasSaved = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        WasSaved = false;
        CloseRequested?.Invoke();
    }

    public AppSettings ApplyTo(AppSettings existing) => new()
    {
        SchemaVersion = existing.SchemaVersion,
        HasCompletedOnboarding = existing.HasCompletedOnboarding,
        BackupRootPath = BackupRootPath,
        ManualSourceRoots = ManualSourceRoots.ToList(),
        WatcherEnabled = WatcherEnabled,
        HourlySnapshotsKept = HourlySnapshotsKept,
        DailySnapshotsKept = DailySnapshotsKept,
        WeeklySnapshotsKept = WeeklySnapshotsKept,
        WatcherDebounceSeconds = WatcherDebounceSeconds,
        GameInstallPath = GameInstallPath,
    };
}
