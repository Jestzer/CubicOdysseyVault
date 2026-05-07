using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core;
using CubicOdysseyVault.Core.Restore;
using CubicOdysseyVault.Core.SaveContent;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;
using CubicOdysseyVault.Core.Steam;
using CubicOdysseyVault.Core.Watching;
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
    private readonly List<SaveWatcher> _watchers = new();
    private List<SaveSource> _activeSources = new();

    public Func<AppSettings, Task<AppSettings?>>? ShowSettingsDialog { get; set; }
    public Func<AppSettings, int, int, int, Task<AppSettings?>>? ShowOnboardingDialog { get; set; }
    public Func<SaveSlot, Snapshot, string, Task<bool>>? ShowRestoreConfirmDialog { get; set; }
    public Func<string, string?, Task<string?>>? ShowTagEditDialog { get; set; }
    public Func<Snapshot, Task<bool>>? ShowDeleteConfirmDialog { get; set; }
    public Func<SaveSlot, SaveSummary, Task>? ShowSaveInspectorDialog { get; set; }
    private ItemCatalog? _itemCatalog;
    public Action<string>? OpenBackupFolderRequested { get; set; }

    public MainWindowViewModel()
    {
        _settings = AppSettingsService.Load();
        _coordinator = new BackupCoordinator(EffectiveBackupRoot(_settings), BuildRetention(_settings));
    }

    [RelayCommand]
    private async Task RefreshDiscoveryAsync()
    {
        if (IsDiscovering) return;

        IsDiscovering = true;
        StatusMessage = "Scanning for Cubic Odyssey saves...";
        StopWatchers();

        try
        {
            SelectedSlot = null;
            var manualRoots = _settings.ManualSourceRoots.ToList();
            var coordinator = _coordinator;

            var result = await Task.Run(() => DiscoverSync(manualRoots, coordinator)).ConfigureAwait(true);

            SteamUsers.Clear();
            DiscoveredSources.Clear();
            foreach (var u in result.Users) SteamUsers.Add(u);
            foreach (var s in result.Sources) DiscoveredSources.Add(s);

            SelectedSteamUser = SteamUsers.FirstOrDefault();
            HasDiscovered = true;
            UpdateShowEmptyState();
            _activeSources = result.RawSources.ToList();
            StartWatchers(_activeSources);
            StatusMessage = BuildSummary(result);

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
    private async Task InspectSelectedSlot()
    {
        if (SelectedSlot == null || ShowSaveInspectorDialog == null) return;
        StatusMessage = "Loading save summary...";
        var slot = SelectedSlot.Slot;
        var (catalog, summary) = await Task.Run(() =>
        {
            var c = EnsureCatalog();
            var s = SaveSummaryBuilder.Build(slot, c);
            return (c, s);
        });
        StatusMessage = catalog.IsEmpty
            ? "Save summary loaded (item catalog not found — names use fallback)."
            : $"Save summary loaded ({catalog.ByIdentifier.Count} items in catalog).";
        await ShowSaveInspectorDialog(slot, summary);
    }

    private ItemCatalog EnsureCatalog()
    {
        if (_itemCatalog != null) return _itemCatalog;

        // Settings override wins over auto-discovery.
        if (!string.IsNullOrEmpty(_settings.GameInstallPath))
        {
            var fromSettings = ItemCatalog.LoadFrom(_settings.GameInstallPath);
            if (!fromSettings.IsEmpty)
            {
                _itemCatalog = fromSettings;
                return _itemCatalog;
            }
        }

        var roots = SteamLocator.Locate();
        var candidates = roots.Select(r =>
            Path.Combine(r.CanonicalPath, Constants.SteamCommonRelative, Constants.CubicOdysseyInstallFolderName));
        _itemCatalog = ItemCatalog.AutoDiscover(candidates);
        return _itemCatalog;
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        var path = EffectiveBackupRoot(_settings);
        try { Directory.CreateDirectory(path); } catch { /* opener may still find a parent */ }
        OpenBackupFolderRequested?.Invoke(path);
    }

    private async Task HandleSlotEditTagAsync(SaveSlot slot, Snapshot snapshot)
    {
        if (ShowTagEditDialog == null) return;
        var label = $"Slot {slot.SlotName} / acct {slot.AccountFolderName} · {snapshot.CapturedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        var newTag = await ShowTagEditDialog(label, snapshot.Tag);
        if (newTag == null) return;

        var ok = await _coordinator.UpdateSlotSnapshotTagAsync(slot, snapshot.Id, newTag);
        if (ok)
        {
            StatusMessage = string.IsNullOrWhiteSpace(newTag)
                ? "Tag cleared."
                : $"Tag set to '{newTag.Trim()}'.";
            await RefreshDiscoveryAsync();
        }
        else
        {
            StatusMessage = "Failed to update tag — snapshot may have been pruned.";
        }
    }

    private async Task HandleSlotDeleteAsync(SaveSlot slot, Snapshot snapshot)
    {
        if (ShowDeleteConfirmDialog == null) return;
        var confirmed = await ShowDeleteConfirmDialog(snapshot);
        if (!confirmed) return;

        var ok = await _coordinator.DeleteSlotSnapshotAsync(slot, snapshot.Id);
        if (ok)
        {
            StatusMessage = "Snapshot deleted.";
            await RefreshDiscoveryAsync();
        }
        else
        {
            StatusMessage = "Failed to delete snapshot.";
        }
    }

    private async Task HandleRestoreSlotAsync(SaveSlot slot, Snapshot snapshot)
    {
        if (ShowRestoreConfirmDialog == null) return;

        var snapshotFolder = _coordinator.GetSlotSnapshotFolder(slot, snapshot);
        var confirmed = await ShowRestoreConfirmDialog(slot, snapshot, snapshotFolder);
        if (!confirmed) return;

        // Pause watchers during the swap so they don't fire spurious Auto
        // snapshots on the rename + copy events. RefreshDiscoveryAsync at
        // the end restarts them.
        StopWatchers();
        StatusMessage = $"Restoring slot {slot.SlotName}...";

        try
        {
            var result = await _coordinator.RestoreSlotAsync(slot, snapshot);
            if (result.Success)
            {
                StatusMessage = $"Restored slot {slot.SlotName} from {snapshot.CapturedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}.";
            }
            else if (result.BlockedByRunningGame)
            {
                StatusMessage = "Restore blocked: Cubic Odyssey is running.";
            }
            else
            {
                StatusMessage = $"Restore failed: {result.Reason}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restore failed: {ex.Message}";
        }

        await RefreshDiscoveryAsync();
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
        var gameInstallChanged = !string.Equals(_settings.GameInstallPath, updated.GameInstallPath, StringComparison.Ordinal);
        _settings = updated;
        AppSettingsService.Save(_settings);
        _coordinator.Update(EffectiveBackupRoot(_settings), BuildRetention(_settings));
        // Drop the cached catalog so the next inspector open reloads from the
        // new path (covers both override-set and override-cleared cases).
        if (gameInstallChanged) _itemCatalog = null;
    }

    private void StartWatchers(IReadOnlyList<SaveSource> sources)
    {
        StopWatchers();
        if (!_settings.WatcherEnabled) return;

        foreach (var src in sources)
        {
            if (!src.Exists || !Directory.Exists(src.RootPath)) continue;

            try
            {
                var capturedSrc = src;
                var w = new SaveWatcher(
                    capturedSrc,
                    TimeSpan.FromSeconds(Math.Max(1, _settings.WatcherDebounceSeconds)),
                    onSlotChanged: key => OnWatcherSlotEvent(capturedSrc, key),
                    onAccountChanged: id => OnWatcherAccountEvent(capturedSrc, id));
                w.Start();
                _watchers.Add(w);
            }
            catch
            {
                // FSW can fail on certain filesystems (network mounts, etc.). Skip silently.
            }
        }
    }

    private void StopWatchers()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
    }

    private void OnWatcherSlotEvent(SaveSource source, SlotKey key)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var vm = SteamUsers
                .FirstOrDefault(u => u.SteamId32 == key.SteamId32)?
                .Slots.FirstOrDefault(s =>
                    s.Slot.AccountFolderName == key.AccountFolder &&
                    s.Slot.SlotName == key.SlotName &&
                    s.Slot.Source.Kind == source.Kind &&
                    s.Slot.Source.RootPath == source.RootPath);
            if (vm == null) return;
            await vm.BackUpAsync(SnapshotTrigger.Auto);
        });
    }

    private void OnWatcherAccountEvent(SaveSource source, string steamId)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var vm = SteamUsers
                .FirstOrDefault(u => u.SteamId32 == steamId)?
                .Accounts.FirstOrDefault(a =>
                    a.Account.Source.Kind == source.Kind &&
                    a.Account.Source.RootPath == source.RootPath);
            if (vm == null) return;
            await vm.BackUpAsync(SnapshotTrigger.Auto);
        });
    }

    private string BuildSummary(DiscoveryResult result)
    {
        var baseMsg = result.Users.Count == 0
            ? "No Cubic Odyssey saves discovered yet."
            : $"Found {result.Users.Count} Steam user{Plural(result.Users.Count)}, " +
              $"{result.TotalSlots} slot{Plural(result.TotalSlots)} across " +
              $"{result.ActiveSources} source{Plural(result.ActiveSources)}.";

        if (_settings.WatcherEnabled && _watchers.Count > 0)
            baseMsg += $" Watching {_watchers.Count} source{Plural(_watchers.Count)}.";
        else if (_settings.WatcherEnabled)
            baseMsg += " Watcher idle.";

        return baseMsg;
    }

    private static string EffectiveBackupRoot(AppSettings settings) =>
        string.IsNullOrEmpty(settings.BackupRootPath)
            ? AppSettingsService.GetSuggestedBackupRoot()
            : settings.BackupRootPath;

    private static RetentionPolicy.Settings BuildRetention(AppSettings s) =>
        new(s.HourlySnapshotsKept, s.DailySnapshotsKept, s.WeeklySnapshotsKept);

    private DiscoveryResult DiscoverSync(IEnumerable<string> manualRoots, BackupCoordinator coordinator)
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
                avm.BackupRequested = (a, t) => coordinator.SnapshotAccountAsync(a, t);
                avm.SetSnapshots(coordinator.ListAccountSnapshots(acct));
            }

            foreach (var slot in layout.Slots)
            {
                var svm = GetOrAdd(byId, slot.SteamId32).AddSlot(slot);
                svm.BackupRequested = (s, t) => coordinator.SnapshotSlotAsync(s, t);
                svm.OnRestoreRequested = HandleRestoreSlotAsync;
                svm.OnEditTagRequested = HandleSlotEditTagAsync;
                svm.OnDeleteRequested = HandleSlotDeleteAsync;
                svm.SetSnapshots(coordinator.ListSlotSnapshots(slot));
                totalSlots++;
            }
        }

        var users = byId.Values.ToList();
        var sourceVms = sources.Select(s => new SaveSourceViewModel(s)).ToList();

        return new DiscoveryResult(users, sourceVms, sources, totalSlots, activeSources);
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
        IReadOnlyList<SaveSource> RawSources,
        int TotalSlots,
        int ActiveSources);
}
