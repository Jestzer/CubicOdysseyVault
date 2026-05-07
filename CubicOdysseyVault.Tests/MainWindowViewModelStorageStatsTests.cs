using System.Collections.Generic;
using CubicOdysseyVault.Core.Snapshots;
using CubicOdysseyVault.UI.ViewModels;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class MainWindowViewModelStorageStatsTests
{
    [Fact]
    public void ComputeStorageStats_EmptyInputs_ReturnsZero()
    {
        var (count, bytes) = MainWindowViewModel.ComputeStorageStats(
            new List<IReadOnlyList<Snapshot>>());
        Assert.Equal(0, count);
        Assert.Equal(0L, bytes);
    }

    [Fact]
    public void ComputeStorageStats_MultipleSlots_SumsAll()
    {
        var slotA = new List<Snapshot>
        {
            new() { TotalBytes = 1000 },
            new() { TotalBytes = 2500 },
        };
        var slotB = new List<Snapshot>
        {
            new() { TotalBytes = 500 },
        };
        var (count, bytes) = MainWindowViewModel.ComputeStorageStats(
            new List<IReadOnlyList<Snapshot>> { slotA, slotB });
        Assert.Equal(3, count);
        Assert.Equal(4000L, bytes);
    }

    [Fact]
    public void FormatBytes_RendersSensibly()
    {
        Assert.Equal("0 B",     MainWindowViewModel.FormatBytes(0));
        Assert.Equal("512 B",   MainWindowViewModel.FormatBytes(512));
        Assert.Equal("1.5 KB",  MainWindowViewModel.FormatBytes(1536));
        Assert.Equal("2.0 MB",  MainWindowViewModel.FormatBytes(2 * 1024 * 1024));
        Assert.Equal("1.5 GB",  MainWindowViewModel.FormatBytes((long)(1.5 * 1024 * 1024 * 1024)));
    }
}
