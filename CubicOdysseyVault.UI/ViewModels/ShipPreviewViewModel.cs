using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CubicOdysseyVault.Core.SaveContent;
using CubicOdysseyVault.Core.Voxels;
using CubicOdysseyVault.UI.Services;

namespace CubicOdysseyVault.UI.ViewModels;

// One row in the SHIPS card. Wraps a ship_*.vx path; on first access of
// Thumbnail / Dim / SolidVoxelCount the file is decoded by BinvoxV3Reader
// and an isometric preview is rendered. Decoding is lazy so opening the
// inspector for a slot with many ships doesn't pay the cost up front for
// rows the user never scrolls into view (the SHIPS card is always
// rendered, but EnsureLoaded is a no-op once loaded so re-virtualization
// from layout passes is cheap).
public partial class ShipPreviewViewModel : ViewModelBase
{
    private const int ThumbnailSize = 96;

    public string FileName { get; }
    public string FullPath { get; }

    [ObservableProperty] private Bitmap? _thumbnail;
    [ObservableProperty] private string _dimensionsLabel = "—";
    [ObservableProperty] private string _solidCountLabel = "—";
    [ObservableProperty] private string _materialsLabel = "—";
    [ObservableProperty] private string? _errorMessage;

    private bool _loaded;

    public ShipPreviewViewModel(ShipFile file)
    {
        FileName = file.FileName;
        FullPath = file.FullPath;
    }

    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            var grid = BinvoxV3Reader.Read(FullPath);
            DimensionsLabel = $"{grid.Dim}³";
            SolidCountLabel = $"{grid.SolidCount:N0} blocks";
            MaterialsLabel = grid.DistinctBlockTypes == 1
                ? "1 material"
                : $"{grid.DistinctBlockTypes} materials";
            Thumbnail = VoxelRenderer.Render(grid, ThumbnailSize, ThumbnailSize);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
