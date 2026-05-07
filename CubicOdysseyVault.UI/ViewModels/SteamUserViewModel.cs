using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CubicOdysseyVault.Core.Saves;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class SteamUserViewModel : ViewModelBase
{
    public string SteamId32 { get; }
    [ObservableProperty] private ObservableCollection<SaveSlotViewModel> _slots = new();
    [ObservableProperty] private ObservableCollection<SaveAccountViewModel> _accounts = new();
    [ObservableProperty] private int _slotCount;

    public SteamUserViewModel(string steamId32)
    {
        SteamId32 = steamId32;
    }

    public SaveAccountViewModel AddAccount(SaveAccount account)
    {
        var vm = new SaveAccountViewModel(account);
        Accounts.Add(vm);
        return vm;
    }

    public SaveSlotViewModel AddSlot(SaveSlot slot)
    {
        var vm = new SaveSlotViewModel(slot);
        Slots.Add(vm);
        SlotCount = Slots.Count;
        return vm;
    }
}
