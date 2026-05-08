using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Snapshots;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class SteamUserViewModel : ViewModelBase
{
    public string SteamId32 { get; }
    [ObservableProperty] private ObservableCollection<SaveSlotViewModel> _slots = new();
    [ObservableProperty] private ObservableCollection<SaveAccountViewModel> _accounts = new();
    [ObservableProperty] private int _slotCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackUpAllCommand))]
    private bool _isBackingUpAll;

    [ObservableProperty] private string? _backUpAllStatus;

    public SteamUserViewModel(string steamId32)
    {
        SteamId32 = steamId32;
    }

    public SaveAccountViewModel AddAccount(SaveAccount account, bool isOrphan = false)
    {
        var vm = new SaveAccountViewModel(account, isOrphan);
        Accounts.Add(vm);
        BackUpAllCommand.NotifyCanExecuteChanged();
        return vm;
    }

    public SaveSlotViewModel AddSlot(SaveSlot slot, bool isOrphan = false)
    {
        var vm = new SaveSlotViewModel(slot, isOrphan);
        Slots.Add(vm);
        SlotCount = Slots.Count;
        BackUpAllCommand.NotifyCanExecuteChanged();
        return vm;
    }

    // Take a manual snapshot of every backable child (account + every slot).
    // Orphans have no live source so we skip them — there's nothing to back up
    // from. Runs sequentially so the status messages and per-VM IsBackingUp
    // flags stay coherent; concurrent backups against the same store can race
    // on manifest writes.
    [RelayCommand(CanExecute = nameof(CanBackUpAll))]
    private async Task BackUpAll()
    {
        if (IsBackingUpAll) return;
        IsBackingUpAll = true;
        BackUpAllStatus = "Backing up everything...";

        int succeeded = 0;
        int skipped = 0;
        int failed = 0;
        try
        {
            foreach (var acct in Accounts.Where(a => !a.IsOrphan))
            {
                await acct.BackUpAsync(SnapshotTrigger.Manual);
                CountResult(acct.BackupStatus, ref succeeded, ref skipped, ref failed);
            }
            foreach (var slot in Slots.Where(s => !s.IsOrphan))
            {
                await slot.BackUpAsync(SnapshotTrigger.Manual);
                CountResult(slot.BackupStatus, ref succeeded, ref skipped, ref failed);
            }
            BackUpAllStatus = BuildSummary(succeeded, skipped, failed);
        }
        finally
        {
            IsBackingUpAll = false;
        }
    }

    private bool CanBackUpAll() =>
        !IsBackingUpAll &&
        (Accounts.Any(a => !a.IsOrphan) || Slots.Any(s => !s.IsOrphan));

    private static void CountResult(string? status, ref int succeeded, ref int skipped, ref int failed)
    {
        if (string.IsNullOrEmpty(status)) { failed++; return; }
        if (status.StartsWith("Saved", StringComparison.Ordinal) ||
            status.StartsWith("Auto-saved", StringComparison.Ordinal)) succeeded++;
        else if (status.StartsWith("No changes", StringComparison.Ordinal)) skipped++;
        else failed++;
    }

    private static string BuildSummary(int succeeded, int skipped, int failed)
    {
        var parts = new List<string>();
        if (succeeded > 0) parts.Add($"{succeeded} saved");
        if (skipped > 0) parts.Add($"{skipped} unchanged");
        if (failed > 0) parts.Add($"{failed} failed");
        return parts.Count == 0 ? "Nothing to back up." : string.Join(" · ", parts);
    }
}
