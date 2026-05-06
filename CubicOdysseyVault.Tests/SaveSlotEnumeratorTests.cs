using System.Linq;
using CubicOdysseyVault.Core.Saves;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class SaveSlotEnumeratorTests
{
    [Fact]
    public void NonExistentSource_ReturnsEmptyLayout_NoThrow()
    {
        var bogus = new SaveSource(
            SaveSourceKind.Manual,
            Path.Combine(Path.GetTempPath(), $"covtest-missing-{Guid.NewGuid():N}"),
            OriginatingSteamRoot: null,
            Exists: false);

        var layout = SaveSlotEnumerator.Enumerate(bogus);
        Assert.Empty(layout.Accounts);
        Assert.Empty(layout.Slots);
    }

    [Fact]
    public void RealLayoutMirror_OneAccountTenSlots()
    {
        using var fixture = new TempLayoutFixture();
        var steamId = fixture.AddSteamId("75412417");
        fixture.AddAccountFile(steamId, "meta.sav", new byte[38]);
        fixture.AddAccountFile(steamId, "93_blueprints.sav", new byte[43]);
        fixture.AddAccountFile(steamId, "93_servers.sav", new byte[31]);
        fixture.AddAccountFile(steamId, "93_stats.sav", new byte[74]);

        var account0 = fixture.AddAccountFolder(steamId, "0");
        for (int i = 0; i < 10; i++)
        {
            var slot = fixture.AddSlot(account0, i.ToString());
            fixture.AddSlotFile(slot, "screenshot.tga", new byte[1024]);
            fixture.AddSlotFile(slot, "93_meta.sav", new byte[200]);
            fixture.AddSlotFile(slot, "93_300000004.vw3", new byte[5000]);
        }

        var layout = SaveSlotEnumerator.Enumerate(fixture.AsManualSource());

        Assert.Single(layout.Accounts);
        var acct = layout.Accounts[0];
        Assert.Equal("75412417", acct.SteamId32);
        Assert.Equal(4, acct.AccountFiles.Count);
        Assert.Equal(38 + 43 + 31 + 74, acct.TotalBytes);

        Assert.Equal(10, layout.Slots.Count);
        Assert.All(layout.Slots, s =>
        {
            Assert.Equal("75412417", s.SteamId32);
            Assert.Equal("0", s.AccountFolderName);
            Assert.True(s.HasScreenshot);
            Assert.Equal(3, s.Files.Count);
            Assert.Equal(1024 + 200 + 5000, s.TotalBytes);
        });

        var slotNames = layout.Slots.Select(s => s.SlotName).OrderBy(n => int.Parse(n)).ToArray();
        Assert.Equal(Enumerable.Range(0, 10).Select(i => i.ToString()).ToArray(), slotNames);
    }

    [Fact]
    public void TwoSteamIds_BothEmittedAsAccounts()
    {
        using var fixture = new TempLayoutFixture();
        var a = fixture.AddSteamId("11111111");
        var b = fixture.AddSteamId("22222222");
        fixture.AddAccountFile(a, "meta.sav", new byte[10]);
        fixture.AddAccountFile(b, "meta.sav", new byte[20]);

        var layout = SaveSlotEnumerator.Enumerate(fixture.AsManualSource());

        Assert.Equal(2, layout.Accounts.Count);
        Assert.Contains(layout.Accounts, x => x.SteamId32 == "11111111" && x.TotalBytes == 10);
        Assert.Contains(layout.Accounts, x => x.SteamId32 == "22222222" && x.TotalBytes == 20);
        Assert.Empty(layout.Slots);
    }

    [Fact]
    public void SteamIdWithNoAccountFolder_StillEmitsAccount()
    {
        using var fixture = new TempLayoutFixture();
        var steamId = fixture.AddSteamId("99999999");
        fixture.AddAccountFile(steamId, "meta.sav", new byte[5]);

        var layout = SaveSlotEnumerator.Enumerate(fixture.AsManualSource());

        Assert.Single(layout.Accounts);
        Assert.Empty(layout.Slots);
    }

    [Fact]
    public void SlotWithoutScreenshot_HasScreenshotFalse()
    {
        using var fixture = new TempLayoutFixture();
        var steamId = fixture.AddSteamId("12345678");
        var account = fixture.AddAccountFolder(steamId, "0");
        var slot = fixture.AddSlot(account, "0");
        fixture.AddSlotFile(slot, "93_meta.sav", new byte[100]);

        var layout = SaveSlotEnumerator.Enumerate(fixture.AsManualSource());

        Assert.Single(layout.Slots);
        Assert.False(layout.Slots[0].HasScreenshot);
    }

    [Fact]
    public void NonNumericTopLevelDirs_AreSkipped()
    {
        using var fixture = new TempLayoutFixture();
        Directory.CreateDirectory(Path.Combine(fixture.RootPath, "junk-dir"));
        var steamId = fixture.AddSteamId("11111111");
        fixture.AddAccountFile(steamId, "meta.sav", new byte[1]);

        var layout = SaveSlotEnumerator.Enumerate(fixture.AsManualSource());

        Assert.Single(layout.Accounts);
        Assert.Equal("11111111", layout.Accounts[0].SteamId32);
    }

    [Fact]
    public void SlotLastWriteUtc_ReflectsNewestFile()
    {
        using var fixture = new TempLayoutFixture();
        var steamId = fixture.AddSteamId("11111111");
        var account = fixture.AddAccountFolder(steamId, "0");
        var slot = fixture.AddSlot(account, "0");
        var older = fixture.AddSlotFile(slot, "old.sav", new byte[10]);
        var newer = fixture.AddSlotFile(slot, "new.sav", new byte[10]);

        File.SetLastWriteTimeUtc(older, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newerStamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(newer, newerStamp);

        var layout = SaveSlotEnumerator.Enumerate(fixture.AsManualSource());

        Assert.Single(layout.Slots);
        Assert.Equal(newerStamp, layout.Slots[0].LastWriteUtc);
    }

    [Fact]
    public void SourceMarkedNotExists_ShortCircuitsToEmpty()
    {
        using var fixture = new TempLayoutFixture();
        var steamId = fixture.AddSteamId("11111111");
        fixture.AddAccountFile(steamId, "meta.sav", new byte[1]);

        var src = new SaveSource(
            SaveSourceKind.Manual,
            fixture.RootPath,
            OriginatingSteamRoot: null,
            Exists: false);

        var layout = SaveSlotEnumerator.Enumerate(src);
        Assert.Empty(layout.Accounts);
        Assert.Empty(layout.Slots);
    }
}

internal sealed class TempLayoutFixture : IDisposable
{
    public string RootPath { get; }

    public TempLayoutFixture()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"covtest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
    }

    public string AddSteamId(string id)
    {
        var dir = Path.Combine(RootPath, id);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string AddAccountFile(string steamIdDir, string fileName, byte[] contents)
    {
        var path = Path.Combine(steamIdDir, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }

    public string AddAccountFolder(string steamIdDir, string accountName)
    {
        var dir = Path.Combine(steamIdDir, accountName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string AddSlot(string accountFolder, string slotName)
    {
        var dir = Path.Combine(accountFolder, slotName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string AddSlotFile(string slotPath, string fileName, byte[] contents)
    {
        var path = Path.Combine(slotPath, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }

    public SaveSource AsManualSource() =>
        new(SaveSourceKind.Manual, RootPath, OriginatingSteamRoot: null, Exists: true);

    public void Dispose()
    {
        try { if (Directory.Exists(RootPath)) Directory.Delete(RootPath, recursive: true); }
        catch { /* swallow */ }
    }
}
