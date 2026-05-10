using System.Collections.Generic;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CubicOdysseyVault.Core.Voxels;
using CubicOdysseyVault.UI.Services;

namespace CubicOdysseyVault.UI.ViewModels;

// One chunk in the map viewer's chunk list. Lazy-loads the chunk's voxels
// and renders both a small thumbnail (for the list row) and a full-size
// top-down preview (for the main pane when this chunk is selected). The
// large render takes the layer-Y range so the layer slider can re-render
// the selected chunk without re-decoding the file.
public partial class WorldChunkPreviewViewModel : ViewModelBase
{
    private const int ThumbnailSize = 96;
    private const int LargeSize = 1024;

    public string FullPath { get; }
    public string FileName { get; }
    public string DisplayName { get; }   // hex chunk-id without the "93_" prefix

    [ObservableProperty] private Bitmap? _thumbnail;
    [ObservableProperty] private Bitmap? _largeBitmap;
    [ObservableProperty] private string _voxelCountLabel = "—";
    [ObservableProperty] private string _materialsLabel = "—";
    [ObservableProperty] private string _yRangeLabel = "—";
    [ObservableProperty] private string? _errorMessage;

    private List<WorldMapRenderer.WorldVoxel>? _voxels;
    public int WorldYMin { get; private set; }
    public int WorldYMax { get; private set; }

    public WorldChunkPreviewViewModel(string fullPath)
    {
        FullPath = fullPath;
        FileName = System.IO.Path.GetFileName(fullPath);
        DisplayName = System.IO.Path.GetFileNameWithoutExtension(fullPath)
            .Replace("93_", "", System.StringComparison.Ordinal);
    }

    // Decode + render the thumbnail. Idempotent.
    public void EnsureLoaded()
    {
        if (_voxels is not null) return;
        try
        {
            var chunk = WorldChunkReader.Read(FullPath);
            // We render in chunk-local coords (no world-offset) because the
            // map view shows one chunk at a time; spatial-position context
            // would need cross-chunk rendering, which isometric did poorly.
            var voxels = new List<WorldMapRenderer.WorldVoxel>(chunk.Voxels.Count);
            int yMin = int.MaxValue, yMax = int.MinValue;
            var distinctTypes = new HashSet<uint>();
            foreach (var v in chunk.Voxels)
            {
                voxels.Add(new WorldMapRenderer.WorldVoxel(v.X, v.Y, v.Z, v.BlockId));
                distinctTypes.Add(v.BlockId >> 24);
                if (v.Y < yMin) yMin = v.Y;
                if (v.Y > yMax) yMax = v.Y;
            }
            _voxels = voxels;
            WorldYMin = yMin == int.MaxValue ? 0 : yMin;
            WorldYMax = yMax == int.MinValue ? 0 : yMax;

            VoxelCountLabel = $"{voxels.Count:N0} blocks";
            MaterialsLabel = distinctTypes.Count == 1 ? "1 material" : $"{distinctTypes.Count} materials";
            YRangeLabel = WorldYMin == WorldYMax ? $"y {WorldYMin}" : $"y {WorldYMin}–{WorldYMax}";

            Thumbnail = WorldMapRenderer.RenderTopDown(
                new WorldMapRenderer.Input(voxels), ThumbnailSize, ThumbnailSize);
        }
        catch (System.Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    // Render or re-render the large preview. Caller invokes when this chunk
    // becomes selected, and again whenever the layer slider moves.
    public void RenderLarge((int min, int max)? yRange = null)
    {
        EnsureLoaded();
        if (_voxels is null) return;
        LargeBitmap = WorldMapRenderer.RenderTopDown(
            new WorldMapRenderer.Input(_voxels), LargeSize, LargeSize, yRange);
    }
}
