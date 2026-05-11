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
using CubicOdysseyVault.Core.Voxels;
using CubicOdysseyVault.Core.Watching;
using CubicOdysseyVault.UI.Services;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private string _statusMessage = "Ready.";
    [ObservableProperty] private ObservableCollection<SteamUserViewModel> _steamUsers = new();
    [ObservableProperty] private SteamUserViewModel? _selectedSteamUser;
    [ObservableProperty] private SaveSlotViewModel? _selectedSlot;
    [ObservableProperty] private SaveAccountViewModel? _selectedAccount;
    [ObservableProperty] private ObservableCollection<SaveSourceViewModel> _discoveredSources = new();
    [ObservableProperty] private bool _isDiscovering;
    [ObservableProperty] private bool _hasDiscovered;
    [ObservableProperty] private bool _showEmptyState;
    [ObservableProperty] private int _totalSnapshotCount;
    [ObservableProperty] private long _totalDiskUsedBytes;
    [ObservableProperty] private string _totalDiskUsedText = "0 B";
    [ObservableProperty] private bool _isWatcherEnabled;
    [ObservableProperty] private int _watcherDebounceSeconds;

    private AppSettings _settings;
    private bool _skipOnboardingThisSession;
    private BackupCoordinator _coordinator;
    private readonly List<SaveWatcher> _watchers = new();
    private List<SaveSource> _activeSources = new();

    public Func<AppSettings, Task<AppSettings?>>? ShowSettingsDialog { get; set; }
    public Func<AppSettings, int, int, int, Task<AppSettings?>>? ShowOnboardingDialog { get; set; }
    public Func<RestoreConfirmViewModel, Task<bool>>? ShowRestoreConfirmDialog { get; set; }
    public Func<string, string?, Task<string?>>? ShowTagEditDialog { get; set; }
    public Func<Snapshot, Task<bool>>? ShowDeleteConfirmDialog { get; set; }
    public Func<SaveSlot, SaveSummary, string?, VoxelTypeCatalog?, Task>? ShowSaveInspectorDialog { get; set; }
    public Func<SaveSlot, VoxelTypeCatalog?, Task>? ShowMapViewerDialog { get; set; }
    private ItemCatalog? _itemCatalog;
    private VoxelTypeCatalog? _voxelCatalog;
    public Action<string>? OpenBackupFolderRequested { get; set; }

    public MainWindowViewModel()
    {
        _settings = AppSettingsService.Load();
        _coordinator = new BackupCoordinator(EffectiveBackupRoot(_settings), BuildRetention(_settings));
        IsWatcherEnabled = _settings.WatcherEnabled;
        WatcherDebounceSeconds = _settings.WatcherDebounceSeconds;
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
            RecomputeStorageStats();
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
        var (catalog, summary, voxelCatalog) = await Task.Run(() =>
        {
            var c = EnsureCatalog();
            var s = SaveSummaryBuilder.Build(slot, c);
            var vc = EnsureVoxelCatalog();
            return (c, s, vc);
        });
        StatusMessage = CatalogStatus(catalog);
        await ShowSaveInspectorDialog(slot, summary, null, voxelCatalog);
    }

    [RelayCommand]
    private async Task ViewSelectedSlotMap()
    {
        if (SelectedSlot == null || ShowMapViewerDialog == null) return;
        var catalog = await Task.Run(EnsureVoxelCatalog);
        await ShowMapViewerDialog(SelectedSlot.Slot, catalog);
    }

    [RelayCommand]
    private async Task InspectSelectedAccount()
    {
        if (SelectedAccount == null || ShowSaveInspectorDialog == null) return;
        StatusMessage = "Loading save summary...";
        var account = SelectedAccount.Account;
        var slot = SynthesiseAccountAsSlot(account.AccountFolderPath, account.SteamId32, account.Source);
        var (catalog, summary, voxelCatalog) = await Task.Run(() =>
        {
            var c = EnsureCatalog();
            var s = SaveSummaryBuilder.Build(slot, c);
            var vc = EnsureVoxelCatalog();
            return (c, s, vc);
        });
        StatusMessage = CatalogStatus(catalog);
        var title = $"Live · Account-level · ({account.SteamId32})";
        await ShowSaveInspectorDialog(slot, summary, title, voxelCatalog);
    }

    private async Task InspectSlotSnapshotAsync(SaveSlot slot, Snapshot snapshot)
    {
        if (ShowSaveInspectorDialog == null) return;
        StatusMessage = "Loading snapshot...";
        var snapshotFolder = _coordinator.GetSlotSnapshotFolder(slot, snapshot);
        if (!Directory.Exists(snapshotFolder))
        {
            StatusMessage = "Snapshot folder no longer exists in the backup store.";
            return;
        }
        var synthSlot = SynthesiseSnapshotAsSlot(snapshotFolder, slot.SteamId32, slot.AccountFolderName, slot.SlotName, slot.Source);
        var (catalog, summary, voxelCatalog) = await Task.Run(() =>
        {
            var c = EnsureCatalog();
            var s = SaveSummaryBuilder.Build(synthSlot, c);
            var vc = EnsureVoxelCatalog();
            return (c, s, vc);
        });
        StatusMessage = CatalogStatus(catalog);
        var title = $"Backup · Slot {slot.SlotName} · acct {slot.AccountFolderName} · {snapshot.CapturedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        await ShowSaveInspectorDialog(synthSlot, summary, title, voxelCatalog);
    }

    private async Task InspectAccountSnapshotAsync(SaveAccount account, Snapshot snapshot)
    {
        if (ShowSaveInspectorDialog == null) return;
        StatusMessage = "Loading snapshot...";
        var snapshotFolder = _coordinator.GetAccountSnapshotFolder(account, snapshot);
        if (!Directory.Exists(snapshotFolder))
        {
            StatusMessage = "Snapshot folder no longer exists in the backup store.";
            return;
        }
        var synthSlot = SynthesiseSnapshotAsSlot(snapshotFolder, account.SteamId32, accountFolderName: "", slotName: "_account", account.Source);
        var (catalog, summary, voxelCatalog) = await Task.Run(() =>
        {
            var c = EnsureCatalog();
            var s = SaveSummaryBuilder.Build(synthSlot, c);
            var vc = EnsureVoxelCatalog();
            return (c, s, vc);
        });
        StatusMessage = CatalogStatus(catalog);
        var title = $"Backup · Account-level · {snapshot.CapturedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        await ShowSaveInspectorDialog(synthSlot, summary, title, voxelCatalog);
    }

    private string CatalogStatus(ItemCatalog catalog) => catalog.IsEmpty
        ? "Save summary loaded (item catalog not found — names use fallback)."
        : $"Save summary loaded ({catalog.ByIdentifier.Count} items in catalog).";

    // Build a synthetic SaveSlot pointing at a snapshot folder so the existing
    // SaveSummaryBuilder + SaveInspectorDialog can operate on it without
    // reaching back into the live filesystem.
    private static SaveSlot SynthesiseSnapshotAsSlot(
        string snapshotFolder,
        string steamId32,
        string accountFolderName,
        string slotName,
        SaveSource source)
    {
        var files = Directory.EnumerateFiles(snapshotFolder)
            .Select(p =>
            {
                var info = new FileInfo(p);
                return new SaveSlotFile(info.Name, info.FullName, info.Length, info.LastWriteTimeUtc);
            })
            .ToList();
        var hasScreenshot = files.Any(f => string.Equals(f.FileName, "screenshot.tga", StringComparison.OrdinalIgnoreCase));
        var lastWrite = files.Count == 0 ? DateTime.UtcNow : files.Max(f => f.LastWriteUtc);
        var totalBytes = files.Sum(f => f.SizeBytes);
        return new SaveSlot(steamId32, accountFolderName, slotName, snapshotFolder, files,
            hasScreenshot, source, lastWrite, totalBytes);
    }

    // Account-level data lives as loose files in the SteamID32 folder. To
    // reuse the slot-shaped inspector machinery, project those files into a
    // synthetic SaveSlot whose "folder" is the account folder itself.
    private static SaveSlot SynthesiseAccountAsSlot(
        string accountFolderPath,
        string steamId32,
        SaveSource source)
    {
        var files = Directory.EnumerateFiles(accountFolderPath)
            .Select(p =>
            {
                var info = new FileInfo(p);
                return new SaveSlotFile(info.Name, info.FullName, info.Length, info.LastWriteTimeUtc);
            })
            .ToList();
        var hasScreenshot = files.Any(f => string.Equals(f.FileName, "screenshot.tga", StringComparison.OrdinalIgnoreCase));
        var lastWrite = files.Count == 0 ? DateTime.UtcNow : files.Max(f => f.LastWriteUtc);
        var totalBytes = files.Sum(f => f.SizeBytes);
        return new SaveSlot(steamId32, "", "_account", accountFolderPath, files,
            hasScreenshot, source, lastWrite, totalBytes);
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

        _itemCatalog = ItemCatalog.AutoDiscover(GameInstallCandidates());
        return _itemCatalog;
    }

    private VoxelTypeCatalog EnsureVoxelCatalog()
    {
        if (_voxelCatalog != null) return _voxelCatalog;

        if (!string.IsNullOrEmpty(_settings.GameInstallPath))
        {
            var fromSettings = VoxelTypeCatalog.LoadFrom(_settings.GameInstallPath);
            if (!fromSettings.IsEmpty)
            {
                _voxelCatalog = fromSettings;
                return _voxelCatalog;
            }
        }

        _voxelCatalog = VoxelTypeCatalog.AutoDiscover(GameInstallCandidates());
        return _voxelCatalog;
    }

    private static IEnumerable<string> GameInstallCandidates()
    {
        var roots = SteamLocator.Locate();
        return roots.Select(r =>
            Path.Combine(r.CanonicalPath, Constants.SteamCommonRelative, Constants.CubicOdysseyInstallFolderName));
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
        var confirmed = await ShowRestoreConfirmDialog(RestoreConfirmViewModel.ForSlot(slot, snapshot, snapshotFolder));
        if (!confirmed) return;

        // Pause watchers during the swap so they don't fire spurious Auto
        // snapshots on the rename + copy events. RefreshDiscoveryAsync at
        // the end restarts them.
        StopWatchers();
        StatusMessage = $"Restoring slot {slot.SlotName}...";

        if (string.IsNullOrEmpty(slot.SlotFolderPath))
        {
            StatusMessage = "Cannot restore: no Steam install detected on this machine. Install Steam, then try again.";
            await RefreshDiscoveryAsync();
            return;
        }

        // Orphan restore: the target folder doesn't exist yet because the user
        // hasn't logged into Steam with this SteamID32 on this machine. We mkdir
        // the parent path so the restore service's existence check passes.
        if (!Directory.Exists(slot.SlotFolderPath))
        {
            try { Directory.CreateDirectory(slot.SlotFolderPath); }
            catch (Exception ex)
            {
                StatusMessage = $"Could not create target folder: {ex.Message}";
                return;
            }
        }

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

    private async Task HandleRestoreAccountAsync(SaveAccount account, Snapshot snapshot)
    {
        if (ShowRestoreConfirmDialog == null) return;

        var snapshotFolder = _coordinator.GetAccountSnapshotFolder(account, snapshot);
        var confirmed = await ShowRestoreConfirmDialog(RestoreConfirmViewModel.ForAccount(account, snapshot, snapshotFolder));
        if (!confirmed) return;

        StopWatchers();
        StatusMessage = $"Restoring account-level data for {account.SteamId32}...";

        if (string.IsNullOrEmpty(account.AccountFolderPath))
        {
            StatusMessage = "Cannot restore: no Steam install detected on this machine. Install Steam, then try again.";
            await RefreshDiscoveryAsync();
            return;
        }

        // Orphan account: target folder may not exist on this machine yet.
        if (!Directory.Exists(account.AccountFolderPath))
        {
            try { Directory.CreateDirectory(account.AccountFolderPath); }
            catch (Exception ex)
            {
                StatusMessage = $"Could not create target folder: {ex.Message}";
                return;
            }
        }

        try
        {
            var result = await _coordinator.RestoreAccountAsync(account, snapshot);
            if (result.Success)
            {
                StatusMessage = $"Restored account-level data from {snapshot.CapturedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}.";
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

    private async Task HandleAccountEditTagAsync(SaveAccount account, Snapshot snapshot)
    {
        if (ShowTagEditDialog == null) return;
        var label = $"Account-level / {account.SteamId32} · {snapshot.CapturedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        var newTag = await ShowTagEditDialog(label, snapshot.Tag);
        if (newTag == null) return;

        var ok = await _coordinator.UpdateAccountSnapshotTagAsync(account, snapshot.Id, newTag);
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

    private async Task HandleAccountDeleteAsync(SaveAccount account, Snapshot snapshot)
    {
        if (ShowDeleteConfirmDialog == null) return;
        var confirmed = await ShowDeleteConfirmDialog(snapshot);
        if (!confirmed) return;

        var ok = await _coordinator.DeleteAccountSnapshotAsync(account, snapshot.Id);
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

    partial void OnSelectedSlotChanged(SaveSlotViewModel? value)
    {
        if (value != null) SelectedAccount = null;
    }

    partial void OnSelectedAccountChanged(SaveAccountViewModel? value)
    {
        if (value != null) SelectedSlot = null;
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
        // Drop the cached catalogs so the next inspector / map open reloads
        // from the new path (covers both override-set and override-cleared
        // cases).
        if (gameInstallChanged)
        {
            _itemCatalog = null;
            _voxelCatalog = null;
        }
        IsWatcherEnabled = _settings.WatcherEnabled;
        WatcherDebounceSeconds = _settings.WatcherDebounceSeconds;
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
        var manualRootsList = manualRoots.ToList();
        var roots = SteamLocator.Locate();
        var sources = SaveLocator.LocateSources(roots, manualRootsList);
        var byId = new Dictionary<string, SteamUserViewModel>();

        // Track which (steamId/account/slot) tuples were seen in live discovery
        // so we can surface only backups that have NO live counterpart as orphans.
        var liveAccountIds = new HashSet<string>(StringComparer.Ordinal);
        var liveSlotKeys = new HashSet<string>(StringComparer.Ordinal);

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
                avm.OnInspectSnapshotRequested = InspectAccountSnapshotAsync;
                avm.OnRestoreRequested = HandleRestoreAccountAsync;
                avm.OnEditTagRequested = HandleAccountEditTagAsync;
                avm.OnDeleteRequested = HandleAccountDeleteAsync;
                avm.SetSnapshots(coordinator.ListAccountSnapshots(acct));
                liveAccountIds.Add(acct.SteamId32);
            }

            foreach (var slot in layout.Slots)
            {
                var svm = GetOrAdd(byId, slot.SteamId32).AddSlot(slot);
                svm.BackupRequested = (s, t) => coordinator.SnapshotSlotAsync(s, t);
                svm.OnInspectSnapshotRequested = InspectSlotSnapshotAsync;
                svm.OnRestoreRequested = HandleRestoreSlotAsync;
                svm.OnEditTagRequested = HandleSlotEditTagAsync;
                svm.OnDeleteRequested = HandleSlotDeleteAsync;
                svm.SetSnapshots(coordinator.ListSlotSnapshots(slot));
                liveSlotKeys.Add(SlotKeyOf(slot.SteamId32, slot.AccountFolderName, slot.SlotName));
                totalSlots++;
            }
        }

        // Backup-only entries: anything in the store with no live counterpart.
        // These get a synthetic SaveAccount / SaveSlot pointing at the would-be
        // live target path so Restore knows where to put files (we mkdir -p
        // before invoking the restore service for orphans).
        var store = coordinator.EnumerateBackupStore();

        foreach (var orphan in store.Accounts)
        {
            if (liveAccountIds.Contains(orphan.SteamId32)) continue;
            var fakeAccount = SynthesiseOrphanAccount(orphan.SteamId32, sources, roots, manualRootsList);
            var avm = GetOrAdd(byId, orphan.SteamId32).AddAccount(fakeAccount, isOrphan: true);
            avm.OnInspectSnapshotRequested = InspectAccountSnapshotAsync;
            avm.OnRestoreRequested = HandleRestoreAccountAsync;
            avm.OnEditTagRequested = HandleAccountEditTagAsync;
            avm.OnDeleteRequested = HandleAccountDeleteAsync;
            avm.SetSnapshots(coordinator.ListAccountSnapshots(fakeAccount));
        }

        foreach (var orphan in store.Slots)
        {
            if (liveSlotKeys.Contains(SlotKeyOf(orphan.SteamId32, orphan.AccountFolderName, orphan.SlotName))) continue;
            var fakeSlot = SynthesiseOrphanSlot(orphan, sources, roots, manualRootsList);
            var svm = GetOrAdd(byId, orphan.SteamId32).AddSlot(fakeSlot, isOrphan: true);
            svm.OnInspectSnapshotRequested = InspectSlotSnapshotAsync;
            svm.OnRestoreRequested = HandleRestoreSlotAsync;
            svm.OnEditTagRequested = HandleSlotEditTagAsync;
            svm.OnDeleteRequested = HandleSlotDeleteAsync;
            svm.SetSnapshots(coordinator.ListSlotSnapshots(fakeSlot));
            totalSlots++;
        }

        var users = byId.Values.ToList();
        var sourceVms = sources.Select(s => new SaveSourceViewModel(s)).ToList();

        return new DiscoveryResult(users, sourceVms, sources, totalSlots, activeSources);
    }

    private static string SlotKeyOf(string steamId, string accountFolderName, string slotName) =>
        $"{steamId} {accountFolderName} {slotName}";

    // Build a SaveAccount whose AccountFolderPath points at where the live
    // folder *would* go on this machine. Always returns a value so the orphan
    // is at least visible/inspectable; the source may be a sentinel
    // (Exists: false, empty RootPath) when neither live sources nor Steam roots
    // are present, in which case Restore is blocked by the handler.
    private static SaveAccount SynthesiseOrphanAccount(
        string steamId32,
        IReadOnlyList<SaveSource> sources,
        IReadOnlyList<SteamRoot> steamRoots,
        IReadOnlyList<string> manualRoots)
    {
        var src = PickOrphanTargetSource(sources, steamRoots, manualRoots);
        var path = string.IsNullOrEmpty(src.RootPath) ? "" : Path.Combine(src.RootPath, steamId32);
        return new SaveAccount(steamId32, path, Array.Empty<SaveAccountFile>(), src, DateTime.UtcNow, 0);
    }

    private static SaveSlot SynthesiseOrphanSlot(
        BackupStoreSlot orphan,
        IReadOnlyList<SaveSource> sources,
        IReadOnlyList<SteamRoot> steamRoots,
        IReadOnlyList<string> manualRoots)
    {
        var src = PickOrphanTargetSource(sources, steamRoots, manualRoots);
        var path = string.IsNullOrEmpty(src.RootPath)
            ? ""
            : Path.Combine(src.RootPath, orphan.SteamId32, orphan.AccountFolderName, orphan.SlotName);
        return new SaveSlot(
            orphan.SteamId32, orphan.AccountFolderName, orphan.SlotName, path,
            Array.Empty<SaveSlotFile>(), HasScreenshot: false,
            src, DateTime.UtcNow, TotalBytes: 0);
    }

    // Find the best place on this machine to restore an orphan into.
    // Prefers a live SaveSource (Cubic Odyssey already runs here), then falls
    // back to constructing a target path from any discovered Steam root, then
    // a manual override path, then a sentinel for "no target found".
    private static SaveSource PickOrphanTargetSource(
        IReadOnlyList<SaveSource> sources,
        IReadOnlyList<SteamRoot> steamRoots,
        IReadOnlyList<string> manualRoots)
    {
        var live = PickLiveSource(sources);
        if (live != null) return live;

        // No live source: build a path under the first Steam root using the
        // platform-appropriate template. Cubic Odyssey doesn't need to be
        // installed yet — Directory.CreateDirectory at restore time will
        // create the chain.
        foreach (var root in steamRoots)
        {
            if (OperatingSystem.IsLinux())
            {
                var saveDir = Path.Combine(
                    root.CanonicalPath,
                    Constants.ProtonCompatdataRelative,
                    Constants.CubicOdysseyAppId.ToString(),
                    Constants.ProtonDocumentsSubpath,
                    Constants.CubicOdysseySaveFolderName,
                    "save");
                return new SaveSource(SaveSourceKind.ProtonCompatdata, saveDir, root, Exists: false);
            }
            if (OperatingSystem.IsWindows())
            {
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(documents)) continue;
                var saveDir = Path.Combine(documents, Constants.CubicOdysseySaveFolderName, "save");
                return new SaveSource(SaveSourceKind.Documents, saveDir, OriginatingSteamRoot: null, Exists: false);
            }
        }

        // No Steam roots either. Last fallback: a manual source root supplied
        // in Settings — we'll restore there. (User opted into that path, so
        // writing into it is consistent with intent.)
        foreach (var manual in manualRoots)
        {
            if (string.IsNullOrWhiteSpace(manual)) continue;
            return new SaveSource(SaveSourceKind.Manual, manual, OriginatingSteamRoot: null, Exists: Directory.Exists(manual));
        }

        // Nothing usable. Caller will see RootPath="" and bail with a status message.
        return new SaveSource(SaveSourceKind.Manual, "", OriginatingSteamRoot: null, Exists: false);
    }

    private static SaveSource? PickLiveSource(IReadOnlyList<SaveSource> sources)
    {
        SaveSource? proton = null;
        SaveSource? documents = null;
        SaveSource? other = null;
        foreach (var s in sources)
        {
            if (!s.Exists) continue;
            switch (s.Kind)
            {
                case SaveSourceKind.ProtonCompatdata: proton ??= s; break;
                case SaveSourceKind.Documents: documents ??= s; break;
                case SaveSourceKind.SteamCloudRemote: other ??= s; break;
            }
        }
        if (OperatingSystem.IsWindows()) return documents ?? other ?? proton;
        return proton ?? documents ?? other;
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

    public static (int count, long bytes) ComputeStorageStats(
        IEnumerable<IReadOnlyList<Snapshot>> snapshotLists)
    {
        int count = 0;
        long bytes = 0;
        foreach (var list in snapshotLists)
        {
            foreach (var s in list)
            {
                count++;
                bytes += s.TotalBytes;
            }
        }
        return (count, bytes);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0.0} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:0.0} MB";
        double gb = mb / 1024.0;
        return $"{gb:0.0} GB";
    }

    private void RecomputeStorageStats()
    {
        var lists = new List<IReadOnlyList<Snapshot>>();
        foreach (var user in SteamUsers)
        {
            foreach (var slot in user.Slots)
                lists.Add(slot.Snapshots.Select(s => s.Snapshot).ToList());
            foreach (var acct in user.Accounts)
                lists.Add(acct.Snapshots.Select(s => s.Snapshot).ToList());
        }
        var (count, bytes) = ComputeStorageStats(lists);
        TotalSnapshotCount = count;
        TotalDiskUsedBytes = bytes;
        TotalDiskUsedText = FormatBytes(bytes);
    }

    private sealed record DiscoveryResult(
        List<SteamUserViewModel> Users,
        List<SaveSourceViewModel> Sources,
        IReadOnlyList<SaveSource> RawSources,
        int TotalSlots,
        int ActiveSources);
}
