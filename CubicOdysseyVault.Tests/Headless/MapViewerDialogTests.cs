using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CubicOdysseyVault.UI.ViewModels;
using CubicOdysseyVault.UI.Views;
using Xunit;
using ZstdSharp;

namespace CubicOdysseyVault.Tests.Headless;

// Headless tests for MapViewerDialog. The dialog is structured as a chunk
// list (left) + selected-chunk top-down render (center) + side panel
// (right). These exercise the full pipeline: synthetic .vw3 files →
// WorldChunkReader → WorldChunkPreviewViewModel → MapViewerViewModel →
// WorldMapRenderer.RenderTopDown → Bitmap.
public class MapViewerDialogTests
{
    [AvaloniaFact]
    public void Open_PopulatesChunkList_AutoSelectsLargest()
    {
        var (dialog, tempDir) = BuildDialog(
            chunks: new[]
            {
                BuildSyntheticChunk(0x300000004UL, voxelCount: 200, distinctTypes: 4),
                BuildSyntheticChunk(0x300000005UL, voxelCount: 50,  distinctTypes: 2),
            });
        try
        {
            dialog.Show();
            ForceLayout(dialog);

            var vm = (MapViewerViewModel)dialog.DataContext!;
            Assert.Equal(2, vm.Chunks.Count);
            Assert.Equal("2 chunks", vm.ChunkCountLabel);

            // Both chunks should have decoded into thumbnails.
            Assert.All(vm.Chunks, c => Assert.NotNull(c.Thumbnail));

            // Auto-select the largest chunk (the 200-voxel one).
            Assert.NotNull(vm.SelectedChunk);
            Assert.Equal("200 blocks", vm.SelectedChunk!.VoxelCountLabel);
            // Selecting a chunk renders its large preview.
            Assert.NotNull(vm.SelectedChunk.LargeBitmap);
        }
        finally
        {
            dialog.Close();
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [AvaloniaFact]
    public void MovingLayerSlider_ReRendersSelectedChunk()
    {
        var (dialog, tempDir) = BuildDialog(
            chunks: new[]
            {
                BuildSyntheticChunkAtSections(0x300000004UL, perSectionCount: 50, sections: new byte[] { 0, 5 }),
            });
        try
        {
            dialog.Show();
            ForceLayout(dialog);

            var vm = (MapViewerViewModel)dialog.DataContext!;
            Assert.NotNull(vm.SelectedChunk);
            var fullRangeHash = HashBitmap(vm.SelectedChunk!.LargeBitmap!);

            // Constrain to the lower band only — voxels at section 5 (Y=160..)
            // should disappear from the rendering.
            vm.YRangeHigh = 31;
            ForceLayout(dialog);
            var lowerOnlyHash = HashBitmap(vm.SelectedChunk.LargeBitmap!);

            Assert.NotEqual(fullRangeHash, lowerOnlyHash);
        }
        finally
        {
            dialog.Close();
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [AvaloniaFact]
    public void ManyBlockVariantsSameType_CollapseToOneMaterial()
    {
        // 100 voxels with the same type byte but distinct full uint32 BlockIds
        // should report "1 material" — guarding the regression where the
        // count showed ~13,000 because variant bytes were treated as distinct.
        var voxels = new List<(byte X, byte YOff, byte Z, byte YSec, uint BlockId)>();
        for (int i = 0; i < 100; i++)
        {
            voxels.Add((X: (byte)i, YOff: (byte)0, Z: (byte)0, YSec: (byte)0,
                BlockId: (0xABu << 24) | (uint)i));
        }
        var (dialog, tempDir) = BuildDialog(
            chunks: new[] { (0x300000004UL, BuildChunkBytes(voxels)) });
        try
        {
            dialog.Show();
            ForceLayout(dialog);
            var vm = (MapViewerViewModel)dialog.DataContext!;
            Assert.NotNull(vm.SelectedChunk);
            Assert.Equal("1 material", vm.SelectedChunk!.MaterialsLabel);
        }
        finally
        {
            dialog.Close();
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [AvaloniaFact]
    public void SwitchingSelectedChunk_UpdatesLargeBitmap()
    {
        var (dialog, tempDir) = BuildDialog(
            chunks: new[]
            {
                BuildSyntheticChunk(0x300000004UL, voxelCount: 50, distinctTypes: 2),
                BuildSyntheticChunk(0x300000005UL, voxelCount: 200, distinctTypes: 5),
            });
        try
        {
            dialog.Show();
            ForceLayout(dialog);

            var vm = (MapViewerViewModel)dialog.DataContext!;
            // Auto-selection picked the larger one (200 voxels).
            var firstSelectedHash = HashBitmap(vm.SelectedChunk!.LargeBitmap!);

            // Switch to the smaller chunk; selecting it triggers RenderLarge
            // for the new chunk.
            var smaller = vm.Chunks.First(c => c.VoxelCountLabel == "50 blocks");
            vm.SelectedChunk = smaller;
            ForceLayout(dialog);

            Assert.NotNull(smaller.LargeBitmap);
            var secondSelectedHash = HashBitmap(smaller.LargeBitmap!);
            Assert.NotEqual(firstSelectedHash, secondSelectedHash);
        }
        finally
        {
            dialog.Close();
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static (MapViewerDialog dialog, string tempDir) BuildDialog(
        IReadOnlyList<(ulong chunkId, byte[] bytes)> chunks)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"co-vault-map-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var paths = new List<string>();
        foreach (var (cid, bytes) in chunks)
        {
            var fn = $"93_{cid - 0x300000000UL:x9}.vw3";
            var p = Path.Combine(tempDir, fn);
            File.WriteAllBytes(p, bytes);
            paths.Add(p);
        }

        var vm = new MapViewerViewModel(paths, "Test");
        var dialog = new MapViewerDialog
        {
            DataContext = vm,
            Width = 1400,
            Height = 900,
        };
        return (dialog, tempDir);
    }

    private static (ulong chunkId, byte[] bytes) BuildSyntheticChunk(ulong chunkId, int voxelCount, int distinctTypes)
    {
        var voxels = new List<(byte X, byte YOff, byte Z, byte YSec, uint BlockId)>();
        for (int i = 0; i < voxelCount; i++)
        {
            byte typeByte = (byte)(0x80u + (chunkId & 0xff) + (uint)(i % distinctTypes));
            voxels.Add((
                X: (byte)(i % 16),
                YOff: (byte)((i / 16) % 32),
                Z: (byte)((i / 256) % 16),
                YSec: 0,
                BlockId: ((uint)typeByte << 24) | (uint)i));
        }
        return (chunkId, BuildChunkBytes(voxels));
    }

    private static (ulong chunkId, byte[] bytes) BuildSyntheticChunkAtSections(ulong chunkId, int perSectionCount, byte[] sections)
    {
        var voxels = new List<(byte X, byte YOff, byte Z, byte YSec, uint BlockId)>();
        foreach (var section in sections)
        {
            for (int i = 0; i < perSectionCount; i++)
            {
                voxels.Add((
                    X: (byte)(i % 16),
                    YOff: (byte)((i / 16) % 32),
                    Z: 0,
                    YSec: section,
                    BlockId: 0xAB000000u));
            }
        }
        return (chunkId, BuildChunkBytes(voxels));
    }

    private static byte[] BuildChunkBytes(List<(byte X, byte YOff, byte Z, byte YSec, uint BlockId)> voxels)
    {
        using var ms = new MemoryStream();
        Span<byte> hdr = stackalloc byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(0,  4), 0x7Bu);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(4,  4), 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(8,  4), 0xC0000000u | (uint)voxels.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(12, 4), 0x2u);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(16, 4), 0u);
        ms.Write(hdr);

        var body = new byte[voxels.Count * 8];
        for (int i = 0; i < voxels.Count; i++)
        {
            var v = voxels[i];
            int o = i * 8;
            body[o + 0] = v.X;
            body[o + 1] = v.YOff;
            body[o + 2] = v.Z;
            body[o + 3] = v.YSec;
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(o + 4, 4), v.BlockId);
        }
        using var compressor = new Compressor();
        var compressed = compressor.Wrap(body).ToArray();
        ms.Write(compressed);
        return ms.ToArray();
    }

    private static void ForceLayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        var size = new Size(window.Width, window.Height);
        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static int HashBitmap(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms);
        var data = ms.ToArray();
        unchecked
        {
            int h = (int)2166136261u;
            foreach (var b in data) { h = (h ^ b) * 16777619; }
            return h;
        }
    }
}
