using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class TagEditViewModel : ViewModelBase
{
    [ObservableProperty] private string _tagValue = "";
    public string SnapshotLabel { get; }

    public bool Confirmed { get; private set; }
    public string? Result { get; private set; }
    public Action? CloseRequested { get; set; }

    public TagEditViewModel(string snapshotLabel, string? currentTag)
    {
        SnapshotLabel = snapshotLabel;
        TagValue = currentTag ?? "";
    }

    [RelayCommand]
    private void Save()
    {
        Confirmed = true;
        Result = TagValue;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Clear()
    {
        Confirmed = true;
        Result = "";
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        Result = null;
        CloseRequested?.Invoke();
    }
}
