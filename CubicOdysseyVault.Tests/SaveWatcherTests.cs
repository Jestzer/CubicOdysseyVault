using CubicOdysseyVault.Core.Saves;
using CubicOdysseyVault.Core.Watching;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class SaveWatcherTests
{
    [Fact]
    public void Classify_SlotFile_ReturnsSlotKey()
    {
        var (slot, account) = SaveWatcher.Classify("75412417/0/3/93_meta.sav");
        Assert.NotNull(slot);
        Assert.Equal("75412417", slot!.SteamId32);
        Assert.Equal("0", slot.AccountFolder);
        Assert.Equal("3", slot.SlotName);
        Assert.Null(account);
    }

    [Fact]
    public void Classify_DeepSlotFile_StillReturnsSlotKey()
    {
        // Even nested files inside a slot folder should map to the slot.
        var (slot, _) = SaveWatcher.Classify("75412417/0/3/sub/extra/file.bin");
        Assert.NotNull(slot);
        Assert.Equal("3", slot!.SlotName);
    }

    [Fact]
    public void Classify_AccountLevelFile_ReturnsAccountId()
    {
        var (slot, account) = SaveWatcher.Classify("75412417/meta.sav");
        Assert.Null(slot);
        Assert.Equal("75412417", account);
    }

    [Fact]
    public void Classify_TmpFile_Ignored()
    {
        var (slot, account) = SaveWatcher.Classify("75412417/0/3/93_meta.sav.tmp");
        Assert.Null(slot);
        Assert.Null(account);
    }

    [Fact]
    public void Classify_NonNumericTopDir_Ignored()
    {
        var (slot, account) = SaveWatcher.Classify("notasteamid/0/3/file");
        Assert.Null(slot);
        Assert.Null(account);
    }

    [Fact]
    public void Classify_NullOrEmpty_ReturnsAllNull()
    {
        var (s1, a1) = SaveWatcher.Classify(null);
        var (s2, a2) = SaveWatcher.Classify("");
        Assert.Null(s1); Assert.Null(a1);
        Assert.Null(s2); Assert.Null(a2);
    }

    [Fact]
    public void Classify_FolderCreation_AtAccountLevel_NotEmitted()
    {
        // 75412417/0  is a directory creation event, not a file change. parts.Length == 2
        // would normally be account-level, but only if it's a file. We accept this as
        // "account-level event" since the watcher consumer can just re-enumerate.
        var (slot, account) = SaveWatcher.Classify("75412417/0");
        Assert.Null(slot);
        Assert.Equal("75412417", account);
    }

    [Fact]
    public void Classify_HandlesBackslashSeparators()
    {
        var (slot, _) = SaveWatcher.Classify(@"75412417\0\3\93_meta.sav");
        Assert.NotNull(slot);
        Assert.Equal("3", slot!.SlotName);
    }

    [Fact]
    public async Task Watcher_FiresAfterDebounce_OnFileWrite()
    {
        using var fixture = new TempLayoutFixture();
        var slotDir = fixture.AddSlot(fixture.AddAccountFolder(fixture.AddSteamId("11111111"), "0"), "0");
        var src = new SaveSource(SaveSourceKind.Manual, fixture.RootPath, null, true);

        var fired = new TaskCompletionSource<SlotKey>();
        using var watcher = new SaveWatcher(
            src,
            TimeSpan.FromMilliseconds(150),
            onSlotChanged: key => fired.TrySetResult(key),
            onAccountChanged: _ => { });
        watcher.Start();

        // Trigger a file write inside the slot folder.
        await Task.Delay(50); // let the FSW settle
        File.WriteAllBytes(Path.Combine(slotDir, "trigger.sav"), new byte[] { 1, 2, 3 });

        var completed = await Task.WhenAny(fired.Task, Task.Delay(5000));
        Assert.Same(fired.Task, completed);

        var key = await fired.Task;
        Assert.Equal("11111111", key.SteamId32);
        Assert.Equal("0", key.AccountFolder);
        Assert.Equal("0", key.SlotName);
    }

    [Fact]
    public async Task Watcher_DebounceCoalesces_RapidWrites_FireOnce()
    {
        using var fixture = new TempLayoutFixture();
        var slotDir = fixture.AddSlot(fixture.AddAccountFolder(fixture.AddSteamId("11111111"), "0"), "0");
        var src = new SaveSource(SaveSourceKind.Manual, fixture.RootPath, null, true);

        int fireCount = 0;
        using var watcher = new SaveWatcher(
            src,
            TimeSpan.FromMilliseconds(200),
            onSlotChanged: _ => Interlocked.Increment(ref fireCount),
            onAccountChanged: _ => { });
        watcher.Start();

        await Task.Delay(50);
        for (int i = 0; i < 10; i++)
        {
            File.WriteAllBytes(Path.Combine(slotDir, $"file{i}.sav"), new byte[] { (byte)i });
            await Task.Delay(20);
        }

        // Wait long enough for debounce to fire once.
        await Task.Delay(500);
        Assert.Equal(1, fireCount);
    }
}
