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

    public void AddAccount(SaveAccount account)
    {
        Accounts.Add(new SaveAccountViewModel(account));
    }

    public void AddSlot(SaveSlot slot)
    {
        Slots.Add(new SaveSlotViewModel(slot));
        SlotCount = Slots.Count;
    }
}
