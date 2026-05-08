using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core.SaveContent;
using CubicOdysseyVault.Core.Saves;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class SaveInspectorViewModel : ViewModelBase
{
    public SaveSlot Slot { get; }
    public SaveSummaryViewModel Summary { get; }
    public string Title { get; }

    [ObservableProperty] private ObservableCollection<SaveFileViewModel> _files = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFile))]
    private SaveFileViewModel? _selectedFile;

    public bool HasSelectedFile => SelectedFile != null;

    public Action? CloseRequested { get; set; }

    public SaveInspectorViewModel(SaveSlot slot, SaveSummary summary, string? title = null)
    {
        Slot = slot;
        Summary = new SaveSummaryViewModel(summary);
        Title = title ?? $"Live · Slot {slot.SlotName} · acct {slot.AccountFolderName}";
        foreach (var f in slot.Files.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase))
            Files.Add(new SaveFileViewModel(f.FullPath, f.SizeBytes));
        SelectedFile = Files.FirstOrDefault();
    }

    partial void OnSelectedFileChanged(SaveFileViewModel? value)
    {
        value?.EnsureLoaded();
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();
}
