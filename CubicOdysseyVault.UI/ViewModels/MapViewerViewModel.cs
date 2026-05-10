using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CubicOdysseyVault.UI.ViewModels;

// Per-chunk top-down map viewer. Each .vw3 in the slot becomes a
// WorldChunkPreviewViewModel with its own thumbnail. Selecting a chunk
// in the list renders it large in the main pane; the layer slider
// adjusts the selected chunk's vertical range and re-renders.
public partial class MapViewerViewModel : ViewModelBase
{
    public string Title { get; }
    public ObservableCollection<WorldChunkPreviewViewModel> Chunks { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedChunk))]
    private WorldChunkPreviewViewModel? _selectedChunk;

    public bool HasSelectedChunk => SelectedChunk is not null;

    [ObservableProperty] private string _chunkCountLabel = "—";
    [ObservableProperty] private string? _errorMessage;

    // Layer slider state — bound to the SELECTED chunk's Y range. Min/Max
    // come from the chunk; Low/High are the user's current selection and
    // re-render on every change.
    [ObservableProperty] private int _yRangeLow;
    [ObservableProperty] private int _yRangeHigh;
    [ObservableProperty] private int _ySliderMin;
    [ObservableProperty] private int _ySliderMax;

    public Action? CloseRequested { get; set; }

    private readonly IReadOnlyList<string> _vw3Paths;

    public MapViewerViewModel(IReadOnlyList<string> vw3Paths, string title)
    {
        _vw3Paths = vw3Paths;
        Title = title;
    }

    public void Load()
    {
        try
        {
            foreach (var path in _vw3Paths)
            {
                var preview = new WorldChunkPreviewViewModel(path);
                preview.EnsureLoaded();
                Chunks.Add(preview);
            }
            ChunkCountLabel = Chunks.Count == 1 ? "1 chunk" : $"{Chunks.Count} chunks";
            // Auto-select the largest chunk (most blocks) as the default —
            // it's almost always the most interesting to look at first.
            WorldChunkPreviewViewModel? largest = null;
            int largestSize = -1;
            foreach (var c in Chunks)
            {
                int size = ParseLeadingInt(c.VoxelCountLabel);
                if (size > largestSize) { largest = c; largestSize = size; }
            }
            if (largest is not null) SelectedChunk = largest;
        }
        catch (System.Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    partial void OnSelectedChunkChanged(WorldChunkPreviewViewModel? value)
    {
        if (value is null) return;
        // Reset slider to the selected chunk's full Y range.
        YSliderMin = value.WorldYMin;
        YSliderMax = value.WorldYMax;
        YRangeLow = value.WorldYMin;
        YRangeHigh = value.WorldYMax;
        value.RenderLarge((value.WorldYMin, value.WorldYMax));
    }

    partial void OnYRangeLowChanged(int value) => RerenderSelected();
    partial void OnYRangeHighChanged(int value) => RerenderSelected();

    private void RerenderSelected()
    {
        if (SelectedChunk is null) return;
        // Don't render if the slider is in an obviously-invalid state
        // (Low > High); that just gives an empty bitmap and confuses users.
        if (YRangeLow > YRangeHigh) return;
        SelectedChunk.RenderLarge((YRangeLow, YRangeHigh));
    }

    // "71,143 blocks" → 71143. Returns 0 on parse failure.
    private static int ParseLeadingInt(string label)
    {
        int n = 0;
        foreach (var c in label)
        {
            if (char.IsDigit(c)) n = n * 10 + (c - '0');
            else if (c == ',') continue;
            else if (n > 0) break;
        }
        return n;
    }
}
