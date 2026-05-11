using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CubicOdysseyVault.Core.Voxels;
using CubicOdysseyVault.UI.Services;

namespace CubicOdysseyVault.UI.ViewModels;

// Top-down map viewer. Each .vw3 in the slot becomes one chunk preview;
// the largest is auto-selected on open. Selecting a chunk renders it in
// the main pane; the layer slider adjusts the selected chunk's vertical
// range and re-renders.
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

    // Discrete zoom for the large render: 1, 2, 4. Higher values produce
    // a larger bitmap so the user can pan around in the ScrollViewer and
    // — once each voxel exceeds TextureZoomThreshold pixels — the renderer
    // switches to its textured path.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    [NotifyPropertyChangedFor(nameof(CanZoomIn))]
    [NotifyPropertyChangedFor(nameof(CanZoomOut))]
    private int _zoomLevel = 1;

    public string ZoomLabel => $"{ZoomLevel}×";
    public bool CanZoomIn => ZoomLevel < MaxZoom;
    public bool CanZoomOut => ZoomLevel > MinZoom;

    private const int MinZoom = 1;
    private const int MaxZoom = 4;

    public Action? CloseRequested { get; set; }

    private readonly IReadOnlyList<string> _vw3Paths;
    private readonly VoxelTypeCatalog? _catalog;
    private readonly VoxelTextureCache? _textures;

    public MapViewerViewModel(IReadOnlyList<string> vw3Paths, string title,
        VoxelTypeCatalog? catalog = null)
    {
        _vw3Paths = vw3Paths;
        Title = title;
        _catalog = catalog;
        _textures = catalog is not null && !catalog.IsEmpty
            ? new VoxelTextureCache(catalog)
            : null;
    }

    public void Load()
    {
        try
        {
            WorldChunkPreviewViewModel? largest = null;
            int largestVoxelCount = -1;
            foreach (var path in _vw3Paths)
            {
                var preview = new WorldChunkPreviewViewModel(path, _catalog, _textures);
                preview.EnsureLoaded();
                Chunks.Add(preview);
                int n = preview.SolidCount;
                if (n > largestVoxelCount) { largestVoxelCount = n; largest = preview; }
            }
            ChunkCountLabel = Chunks.Count == 1 ? "1 chunk" : $"{Chunks.Count} chunks";
            // Auto-select the largest chunk — almost always the most
            // interesting starting point.
            SelectedChunk = largest ?? Chunks.FirstOrDefault();
        }
        catch (System.Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    [RelayCommand]
    private void ZoomIn()
    {
        if (CanZoomIn) ZoomLevel = System.Math.Min(MaxZoom, ZoomLevel * 2);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        if (CanZoomOut) ZoomLevel = System.Math.Max(MinZoom, ZoomLevel / 2);
    }

    partial void OnSelectedChunkChanged(WorldChunkPreviewViewModel? value)
    {
        if (value is null) return;
        // Reset slider to the selected chunk's full Y range.
        YSliderMin = value.WorldYMin;
        YSliderMax = value.WorldYMax;
        YRangeLow = value.WorldYMin;
        YRangeHigh = value.WorldYMax;
        value.RenderLarge((value.WorldYMin, value.WorldYMax), ZoomLevel);
    }

    partial void OnYRangeLowChanged(int value) => RerenderSelected();
    partial void OnYRangeHighChanged(int value) => RerenderSelected();
    partial void OnZoomLevelChanged(int value) => RerenderSelected();

    private void RerenderSelected()
    {
        if (SelectedChunk is null) return;
        // Don't render if the slider is in an obviously-invalid state
        // (Low > High); that just gives an empty bitmap and confuses users.
        if (YRangeLow > YRangeHigh) return;
        SelectedChunk.RenderLarge((YRangeLow, YRangeHigh), ZoomLevel);
    }
}
