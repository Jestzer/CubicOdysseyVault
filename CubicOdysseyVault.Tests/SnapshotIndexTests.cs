using CubicOdysseyVault.Core.Integrity;
using CubicOdysseyVault.Core.Snapshots;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class SnapshotIndexTests
{
    [Fact]
    public void Load_FromMissingFile_ReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"covtest-{Guid.NewGuid():N}.json");
        var manifest = SnapshotIndex.Load(path);
        Assert.Empty(manifest.Snapshots);
        Assert.Equal(1, manifest.SchemaVersion);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"covtest-{Guid.NewGuid():N}.json");
        try
        {
            var original = new SnapshotManifest
            {
                Snapshots =
                {
                    new Snapshot
                    {
                        Id = "2026-05-06T14-30-12Z__abc123",
                        CapturedAtUtc = new DateTime(2026, 5, 6, 14, 30, 12, DateTimeKind.Utc),
                        Trigger = SnapshotTrigger.Manual,
                        Tag = "before raid",
                        CombinedHash = "sha256:abc123",
                        FileHashes = new() { ["meta.sav"] = "deadbeef", ["screenshot.tga"] = "cafef00d" },
                        TotalBytes = 1024,
                        Health = SlotHealth.Healthy,
                        SourceKind = "ProtonCompatdata",
                        FolderName = "2026-05-06T14-30-12Z__abc123",
                    },
                },
            };

            SnapshotIndex.Save(original, path);
            var loaded = SnapshotIndex.Load(path);

            Assert.Single(loaded.Snapshots);
            var s = loaded.Snapshots[0];
            Assert.Equal("2026-05-06T14-30-12Z__abc123", s.Id);
            Assert.Equal(SnapshotTrigger.Manual, s.Trigger);
            Assert.Equal("before raid", s.Tag);
            Assert.Equal("sha256:abc123", s.CombinedHash);
            Assert.Equal(2, s.FileHashes.Count);
            Assert.Equal("deadbeef", s.FileHashes["meta.sav"]);
            Assert.Equal(1024, s.TotalBytes);
            Assert.Equal(SlotHealth.Healthy, s.Health);
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void Save_CreatesParentDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"covtest-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "nested", "manifest.json");
        try
        {
            SnapshotIndex.Save(new SnapshotManifest(), path);
            Assert.True(File.Exists(path));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void Load_FromCorruptJson_ReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"covtest-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ this is not valid json");
            var manifest = SnapshotIndex.Load(path);
            Assert.Empty(manifest.Snapshots);
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void Save_UsesAtomicTmpRename_NoTmpLeftOver()
    {
        var path = Path.Combine(Path.GetTempPath(), $"covtest-{Guid.NewGuid():N}.json");
        try
        {
            SnapshotIndex.Save(new SnapshotManifest(), path);
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
