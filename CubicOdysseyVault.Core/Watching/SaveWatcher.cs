using CubicOdysseyVault.Core.Saves;

namespace CubicOdysseyVault.Core.Watching;

// Watches a SaveSource for filesystem changes and emits per-slot or
// per-account events after a debounce window. Each SlotKey gets its own
// debounce timer, so unrelated slots don't gate each other. The same
// applies per SteamID32 for account-level files. Callbacks fire on
// thread-pool threads — consumers must marshal to UI thread if needed.
public sealed class SaveWatcher : IDisposable
{
    private readonly FileSystemWatcher _fsw;
    private readonly TimeSpan _debounce;
    private readonly Action<SlotKey> _onSlotChanged;
    private readonly Action<string> _onAccountChanged;
    private readonly object _lock = new();
    private readonly Dictionary<SlotKey, Timer> _slotTimers = new();
    private readonly Dictionary<string, Timer> _accountTimers = new();
    private bool _disposed;

    public SaveSource Source { get; }
    public bool IsActive => !_disposed && _fsw.EnableRaisingEvents;

    public SaveWatcher(
        SaveSource source,
        TimeSpan debounce,
        Action<SlotKey> onSlotChanged,
        Action<string> onAccountChanged)
    {
        Source = source;
        _debounce = debounce;
        _onSlotChanged = onSlotChanged;
        _onAccountChanged = onAccountChanged;

        _fsw = new FileSystemWatcher(source.RootPath)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = false,
            NotifyFilter = NotifyFilters.LastWrite
                         | NotifyFilters.FileName
                         | NotifyFilters.CreationTime
                         | NotifyFilters.Size,
        };
        _fsw.Changed += HandleEvent;
        _fsw.Created += HandleEvent;
        _fsw.Deleted += HandleEvent;
        _fsw.Renamed += HandleRenamed;
    }

    public void Start()
    {
        if (_disposed) return;
        if (!Directory.Exists(Source.RootPath)) return;
        _fsw.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        if (_disposed) return;
        _fsw.EnableRaisingEvents = false;
    }

    private void HandleEvent(object sender, FileSystemEventArgs e) =>
        ProcessChange(e.Name);

    private void HandleRenamed(object sender, RenamedEventArgs e)
    {
        ProcessChange(e.OldName);
        ProcessChange(e.Name);
    }

    private void ProcessChange(string? relativePath)
    {
        var (slot, accountId) = Classify(relativePath);
        if (slot != null) DebounceSlot(slot);
        else if (accountId != null) DebounceAccount(accountId);
    }

    internal static (SlotKey? Slot, string? AccountId) Classify(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return (null, null);
        if (relativePath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return (null, null);

        // Accept both separators: FSW on Linux/macOS emits forward slashes, Windows
        // emits backslashes — but tests and manual callers may pass either form.
        var parts = relativePath.Split(
            new[] { '/', '\\' },
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 || !IsAllDigits(parts[0])) return (null, null);

        if (parts.Length >= 4)
            return (new SlotKey(parts[0], parts[1], parts[2]), null);
        if (parts.Length == 2)
            return (null, parts[0]);
        return (null, null);
    }

    private void DebounceSlot(SlotKey key)
    {
        lock (_lock)
        {
            if (_disposed) return;
            if (_slotTimers.TryGetValue(key, out var existing))
            {
                existing.Change(_debounce, Timeout.InfiniteTimeSpan);
                return;
            }
            var timer = new Timer(_ =>
            {
                lock (_lock)
                {
                    if (_disposed) return;
                    _slotTimers.Remove(key);
                }
                try { _onSlotChanged(key); } catch { /* swallow; consumer faults shouldn't kill the watcher */ }
            }, null, _debounce, Timeout.InfiniteTimeSpan);
            _slotTimers[key] = timer;
        }
    }

    private void DebounceAccount(string steamId)
    {
        lock (_lock)
        {
            if (_disposed) return;
            if (_accountTimers.TryGetValue(steamId, out var existing))
            {
                existing.Change(_debounce, Timeout.InfiniteTimeSpan);
                return;
            }
            var timer = new Timer(_ =>
            {
                lock (_lock)
                {
                    if (_disposed) return;
                    _accountTimers.Remove(steamId);
                }
                try { _onAccountChanged(steamId); } catch { }
            }, null, _debounce, Timeout.InfiniteTimeSpan);
            _accountTimers[steamId] = timer;
        }
    }

    private static bool IsAllDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (var c in s) if (!char.IsDigit(c)) return false;
        return true;
    }

    public void Dispose()
    {
        Timer[] slot;
        Timer[] account;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            slot = _slotTimers.Values.ToArray();
            account = _accountTimers.Values.ToArray();
            _slotTimers.Clear();
            _accountTimers.Clear();
        }
        try { _fsw.EnableRaisingEvents = false; } catch { }
        _fsw.Dispose();
        foreach (var t in slot) t.Dispose();
        foreach (var t in account) t.Dispose();
    }
}
