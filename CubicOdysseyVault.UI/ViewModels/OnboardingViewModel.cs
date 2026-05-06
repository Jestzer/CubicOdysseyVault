using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.UI.Services;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class OnboardingViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1), nameof(IsStep2))]
    private int _currentStep = 1;

    [ObservableProperty] private string _detectedSummary = "";

    public SettingsViewModel SettingsForm { get; }

    public bool WasCompleted { get; private set; }

    public Func<Task<string?>>? PickFolderRequested
    {
        get => SettingsForm.PickFolderRequested;
        set => SettingsForm.PickFolderRequested = value;
    }
    public Action? CloseRequested { get; set; }

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;

    public OnboardingViewModel(AppSettings settings, int discoveredUsers, int discoveredSlots, int activeSources)
    {
        SettingsForm = new SettingsViewModel(settings);
        if (string.IsNullOrEmpty(SettingsForm.BackupRootPath))
            SettingsForm.BackupRootPath = AppSettingsService.GetSuggestedBackupRoot();

        DetectedSummary = BuildSummary(discoveredUsers, discoveredSlots, activeSources);
    }

    private static string BuildSummary(int users, int slots, int sources)
    {
        if (users == 0)
            return "No Cubic Odyssey saves found yet — that's fine. The vault will pick them up the moment you play.";
        return $"Found {slots} slot{(slots == 1 ? "" : "s")} for {users} Steam user{(users == 1 ? "" : "s")} across {sources} source{(sources == 1 ? "" : "s")}.";
    }

    [RelayCommand]
    private void Next() { if (CurrentStep == 1) CurrentStep = 2; }

    [RelayCommand]
    private void Back() { if (CurrentStep == 2) CurrentStep = 1; }

    [RelayCommand]
    private void Finish()
    {
        WasCompleted = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        WasCompleted = false;
        CloseRequested?.Invoke();
    }

    public AppSettings ApplyTo(AppSettings existing)
    {
        var s = SettingsForm.ApplyTo(existing);
        s.HasCompletedOnboarding = true;
        return s;
    }
}
