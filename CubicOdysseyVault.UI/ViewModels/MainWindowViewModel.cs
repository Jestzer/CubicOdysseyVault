using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;
using CubicOdysseyVault.Core.Steam;
using CubicOdysseyVault.UI.Services;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private string _statusMessage = "Ready.";
    [ObservableProperty] private ObservableCollection<SteamUserViewModel> _steamUsers = new();
    [ObservableProperty] private SteamUserViewModel? _selectedSteamUser;
    [ObservableProperty] private SaveSlotViewModel? _selectedSlot;
    [ObservableProperty] private ObservableCollection<SaveSourceViewModel> _discoveredSources = new();
    [ObservableProperty] private bool _isDiscovering;
    [ObservableProperty] private bool _hasDiscovered;
    [ObservableProperty] private bool _showEmptyState;

    private AppSettings _settings;
    private bool _skipOnboardingThisSession;
    private BackupCoordinator _coordinator;

    public Func<AppSettings, Task<AppSettings?>>? ShowSettingsDialog { get; set; }
    public Func<AppSettings, int, int, int, Task<AppSettings?>>? ShowOnboardingDialog { get; set; }
    public Action<string>? OpenBackupFolderRequested { get; set; }

    public MainWindowViewModel()
    {
        _settings = AppSettingsService.Load();
        _coordinator = new BackupCoordinator(EffectiveBackupRoot(_settings));
    }

    [RelayCommand]
    private async Task RefreshDiscoveryAsync()
    {
        if (IsDiscovering) return;

        IsDiscovering = true;
        StatusMessage = "Scanning for Cubic Odyssey saves...";

        try
        {
            SelectedSlot = null;
            var manualRoots = _settings.ManualSourceRoots.ToList();
            var coordinator = _coordinator;

            var result = await Task.Run(() => DiscoverSync(manualRoots, coordinator));

            SteamUsers.Clear();
            DiscoveredSources.Clear();
            foreach (var u in result.Users) SteamUsers.Add(u);
            foreach (var s in result.Sources) DiscoveredSources.Add(s);

            SelectedSteamUser = SteamUsers.FirstOrDefault();
            HasDiscovered = true;
            UpdateShowEmptyState();
            StatusMessage = result.Summary;

            if (!_settings.HasCompletedOnboarding && !_skipOnboardingThisSession)
                await TryShowOnboarding(result.Users.Count, result.TotalSlots, result.ActiveSources);
        }
        catch (Exception ex)
        {
            HasDiscovered = true;
            UpdateShowEmptyState();
            StatusMessage = $"Discovery failed: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        if (ShowSettingsDialog == null) return;
        var updated = await ShowSettingsDialog(_settings);
        if (updated != null)
        {
            ApplySettings(updated);
            StatusMessage = "Settings saved. Re-scanning...";
            await RefreshDiscoveryAsync();
        }
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        var path = EffectiveBackupRoot(_settings);
        try { Directory.CreateDirectory(path); } catch { /* opener may still find a parent */ }
        OpenBackupFolderRequested?.Invoke(path);
    }

    partial void OnHasDiscoveredChanged(bool value) => UpdateShowEmptyState();

    private void UpdateShowEmptyState() =>
        ShowEmptyState = HasDiscovered && SteamUsers.Count == 0;

    private async Task TryShowOnboarding(int users, int slots, int sources)
    {
        if (ShowOnboardingDialog == null) return;
        var updated = await ShowOnboardingDialog(_settings, users, slots, sources);
        if (updated != null)
        {
            ApplySettings(updated);
            StatusMessage = "Vault configured. Re-scanning with your settings...";
            await RefreshDiscoveryAsync();
        }
        else
        {
            _skipOnboardingThisSession = true;
        }
    }

    private void ApplySettings(AppSettings updated)
    {
        _settings = updated;
        AppSettingsService.Save(_settings);
        _coordinator.UpdateBackupRoot(EffectiveBackupRoot(_settings));
    }

    private static string EffectiveBackupRoot(AppSettings settings) =>
        string.IsNullOrEmpty(settings.BackupRootPath)
            ? AppSettingsService.GetSuggestedBackupRoot()
            : settings.BackupRootPath;

    private static DiscoveryResult DiscoverSync(IEnumerable<string> manualRoots, BackupCoordinator coordinator)
    {
        var roots = SteamLocator.Locate();
        var sources = SaveLocator.LocateSources(roots, manualRoots);
        var byId = new Dictionary<string, SteamUserViewModel>();

        int totalSlots = 0;
        int activeSources = 0;

        foreach (var src in sources)
        {
            var layout = SaveSlotEnumerator.Enumerate(src);
            if (layout.Accounts.Count > 0 || layout.Slots.Count > 0) activeSources++;

            foreach (var acct in layout.Accounts)
            {
                var avm = GetOrAdd(byId, acct.SteamId32).AddAccount(acct);
                avm.BackupRequested = a => coordinator.SnapshotAccountAsync(a, SnapshotTrigger.Manual);
                avm.SetSnapshots(coordinator.ListAccountSnapshots(acct));
            }

            foreach (var slot in layout.Slots)
            {
                var svm = GetOrAdd(byId, slot.SteamId32).AddSlot(slot);
                svm.BackupRequested = s => coordinator.SnapshotSlotAsync(s, SnapshotTrigger.Manual);
                svm.SetSnapshots(coordinator.ListSlotSnapshots(slot));
                totalSlots++;
            }
        }

        var users = byId.Values.ToList();
        var sourceVms = sources.Select(s => new SaveSourceViewModel(s)).ToList();

        string summary = users.Count == 0
            ? "No Cubic Odyssey saves discovered yet."
            : $"Found {users.Count} Steam user{Plural(users.Count)}, " +
              $"{totalSlots} slot{Plural(totalSlots)} across " +
              $"{activeSources} source{Plural(activeSources)}.";

        return new DiscoveryResult(users, sourceVms, summary, totalSlots, activeSources);
    }

    private static SteamUserViewModel GetOrAdd(Dictionary<string, SteamUserViewModel> dict, string id)
    {
        if (!dict.TryGetValue(id, out var user))
        {
            user = new SteamUserViewModel(id);
            dict[id] = user;
        }
        return user;
    }

    private static string Plural(int n) => n == 1 ? string.Empty : "s";

    private sealed record DiscoveryResult(
        List<SteamUserViewModel> Users,
        List<SaveSourceViewModel> Sources,
        string Summary,
        int TotalSlots,
        int ActiveSources);
}
