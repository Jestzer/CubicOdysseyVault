using System.Linq;
using CubicOdysseyVault.Core.Integrity;
using CubicOdysseyVault.Core.Snapshots;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class RetentionPolicyTests
{
    private static readonly DateTime Now = new(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EmptyInput_KeepsAndPrunesNothing()
    {
        var plan = RetentionPolicy.Apply(Array.Empty<Snapshot>(), RetentionPolicy.Settings.Default, Now);
        Assert.Empty(plan.Keep);
        Assert.Empty(plan.Prune);
    }

    [Fact]
    public void ManualSnapshots_AlwaysKept_EvenIfAncient()
    {
        var ancient = MakeSnapshot(Now.AddYears(-3), SnapshotTrigger.Manual);
        var plan = RetentionPolicy.Apply(new[] { ancient }, RetentionPolicy.Settings.Default, Now);
        Assert.Contains(ancient, plan.Keep);
        Assert.Empty(plan.Prune);
    }

    [Fact]
    public void PreRestoreSnapshots_AlwaysKept()
    {
        var ancient = MakeSnapshot(Now.AddDays(-365), SnapshotTrigger.PreRestore);
        var plan = RetentionPolicy.Apply(new[] { ancient }, RetentionPolicy.Settings.Default, Now);
        Assert.Contains(ancient, plan.Keep);
        Assert.Empty(plan.Prune);
    }

    [Fact]
    public void TaggedAutoSnapshots_AreKept()
    {
        var tagged = MakeSnapshot(Now.AddDays(-300), SnapshotTrigger.Auto, tag: "milestone-boss-1");
        var plan = RetentionPolicy.Apply(new[] { tagged }, RetentionPolicy.Settings.Default, Now);
        Assert.Contains(tagged, plan.Keep);
    }

    [Fact]
    public void AutoSnapshots_WithinHourlyWindow_OnePerHourKept()
    {
        // Now = 12:00. 5 snapshots spread across 3 hour buckets.
        var snaps = new[]
        {
            MakeSnapshot(Now.AddMinutes(-10), SnapshotTrigger.Auto),         // 11:50 → hour 11:00 (kept, newest in bucket)
            MakeSnapshot(Now.AddMinutes(-30), SnapshotTrigger.Auto),         // 11:30 → hour 11:00 (pruned, duplicate)
            MakeSnapshot(Now.AddHours(-1).AddMinutes(-5), SnapshotTrigger.Auto), // 10:55 → hour 10:00
            MakeSnapshot(Now.AddHours(-2).AddMinutes(-5), SnapshotTrigger.Auto), // 9:55 → hour 9:00 (kept)
            MakeSnapshot(Now.AddHours(-2).AddMinutes(-30), SnapshotTrigger.Auto), // 9:30 → hour 9:00 (pruned, duplicate)
        };

        var plan = RetentionPolicy.Apply(snaps, new RetentionPolicy.Settings(24, 14, 8), Now);

        Assert.Equal(3, plan.Keep.Count);
        Assert.Equal(2, plan.Prune.Count);
        Assert.Contains(snaps[1], plan.Prune);
        Assert.Contains(snaps[4], plan.Prune);
    }

    [Fact]
    public void AutoSnapshots_BeyondHourlyWindow_FallToDaily_OnePerDay()
    {
        // Now = 2026-05-06 12:00. Hourly cutoff = 2026-05-05 12:00.
        // Two auto snapshots on the same day-bucket (2026-05-04), both before the hourly cutoff.
        var snaps = new[]
        {
            MakeSnapshot(new DateTime(2026, 5, 4, 14, 0, 0, DateTimeKind.Utc), SnapshotTrigger.Auto),
            MakeSnapshot(new DateTime(2026, 5, 4, 8, 0, 0, DateTimeKind.Utc), SnapshotTrigger.Auto),
            MakeSnapshot(new DateTime(2026, 5, 3, 14, 0, 0, DateTimeKind.Utc), SnapshotTrigger.Auto),
        };

        var plan = RetentionPolicy.Apply(snaps, new RetentionPolicy.Settings(24, 14, 8), Now);

        Assert.Equal(2, plan.Keep.Count);
        Assert.Single(plan.Prune);
        // Older same-day entry (08:00) should be pruned, newer (14:00) kept
        Assert.Equal(snaps[1], plan.Prune[0]);
    }

    [Fact]
    public void AutoSnapshots_BeyondDailyWindow_FallToWeekly()
    {
        // Settings: 1 hour, 2 days, 4 weeks. Snapshots spread far back.
        var settings = new RetentionPolicy.Settings(1, 2, 4);
        // hourCutoff = Now - 1h = 11:00
        // dayCutoff  = Now - 2d = 2026-05-04 12:00
        // weekCutoff = Now - 4w = 2026-04-08 12:00

        var snaps = new[]
        {
            MakeSnapshot(Now.AddDays(-3), SnapshotTrigger.Auto),     // beyond daily, in weekly
            MakeSnapshot(Now.AddDays(-3).AddHours(-3), SnapshotTrigger.Auto),  // same week as above; should prune
            MakeSnapshot(Now.AddDays(-10), SnapshotTrigger.Auto),    // earlier week
            MakeSnapshot(Now.AddDays(-40), SnapshotTrigger.Auto),    // beyond weekly, prune
        };

        var plan = RetentionPolicy.Apply(snaps, settings, Now);
        // First and third in distinct week buckets → kept (2). Second is duplicate week → pruned. Fourth beyond cutoff → pruned.
        Assert.Equal(2, plan.Keep.Count);
        Assert.Equal(2, plan.Prune.Count);
        Assert.Contains(snaps[1], plan.Prune);
        Assert.Contains(snaps[3], plan.Prune);
    }

    [Fact]
    public void MixedAutoAndManual_AutoPrunedAtBoundary_ManualKept()
    {
        var snaps = new[]
        {
            MakeSnapshot(Now.AddDays(-100), SnapshotTrigger.Manual),  // ancient manual — kept
            MakeSnapshot(Now.AddDays(-100), SnapshotTrigger.Auto),    // ancient auto — pruned (beyond all windows)
            MakeSnapshot(Now.AddMinutes(-5), SnapshotTrigger.Auto),   // recent auto — kept (hourly)
        };
        var plan = RetentionPolicy.Apply(snaps, RetentionPolicy.Settings.Default, Now);

        Assert.Equal(2, plan.Keep.Count);
        Assert.Single(plan.Prune);
        Assert.Equal(snaps[1], plan.Prune[0]);
    }

    [Fact]
    public void DefaultSettings_Are24_14_8()
    {
        Assert.Equal(24, RetentionPolicy.Settings.Default.HourlyKept);
        Assert.Equal(14, RetentionPolicy.Settings.Default.DailyKept);
        Assert.Equal(8, RetentionPolicy.Settings.Default.WeeklyKept);
    }

    private static Snapshot MakeSnapshot(DateTime atUtc, SnapshotTrigger trigger, string? tag = null) => new()
    {
        Id = atUtc.ToString("yyyy-MM-ddTHH-mm-ssZ") + "__" + Guid.NewGuid().ToString("N")[..6],
        CapturedAtUtc = atUtc,
        Trigger = trigger,
        Tag = tag,
        CombinedHash = "sha256:" + Guid.NewGuid().ToString("N"),
        FileHashes = new(),
        TotalBytes = 100,
        Health = SlotHealth.Healthy,
        SourceKind = "ProtonCompatdata",
        FolderName = atUtc.ToString("yyyy-MM-ddTHH-mm-ssZ") + "__test",
    };
}
