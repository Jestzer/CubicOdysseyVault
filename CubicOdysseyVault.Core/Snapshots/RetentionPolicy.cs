using System.Linq;

namespace CubicOdysseyVault.Core.Snapshots;

// Tiered/generational pruning: keep one Auto snapshot per hour for the last
// HourlyKept hours, one per day for the last DailyKept days (older than the
// hourly window), and one per week for the last WeeklyKept weeks (older than
// the daily window). Manual + PreRestore + any tagged snapshot is kept
// forever. Older Auto snapshots fall off the bottom.
public static class RetentionPolicy
{
    public sealed record Settings(int HourlyKept, int DailyKept, int WeeklyKept)
    {
        public static Settings Default { get; } = new(24, 14, 8);
    }

    public sealed record Plan(IReadOnlyList<Snapshot> Keep, IReadOnlyList<Snapshot> Prune);

    public static Plan Apply(IEnumerable<Snapshot> snapshots, Settings settings, DateTime nowUtc)
    {
        var all = snapshots.ToList();
        var keep = new HashSet<Snapshot>();

        foreach (var s in all)
        {
            if (s.Trigger == SnapshotTrigger.Manual ||
                s.Trigger == SnapshotTrigger.PreRestore ||
                !string.IsNullOrEmpty(s.Tag))
            {
                keep.Add(s);
            }
        }

        var autos = all
            .Where(s => s.Trigger == SnapshotTrigger.Auto && string.IsNullOrEmpty(s.Tag))
            .OrderByDescending(s => s.CapturedAtUtc)
            .ToList();

        var hourCutoff = nowUtc.AddHours(-settings.HourlyKept);
        var dayCutoff = nowUtc.AddDays(-settings.DailyKept);
        var weekCutoff = nowUtc.AddDays(-7 * settings.WeeklyKept);

        var hourlyBuckets = new HashSet<DateTime>();
        var dailyBuckets = new HashSet<DateTime>();
        var weeklyBuckets = new HashSet<DateTime>();

        foreach (var s in autos)
        {
            var ts = s.CapturedAtUtc;
            if (ts >= hourCutoff)
            {
                if (hourlyBuckets.Add(TruncateToHour(ts))) keep.Add(s);
            }
            else if (ts >= dayCutoff)
            {
                if (dailyBuckets.Add(TruncateToDay(ts))) keep.Add(s);
            }
            else if (ts >= weekCutoff)
            {
                if (weeklyBuckets.Add(TruncateToWeek(ts))) keep.Add(s);
            }
            // anything older than weekCutoff falls through and is pruned
        }

        var prune = all.Where(s => !keep.Contains(s)).ToList();
        var keepList = all.Where(keep.Contains).ToList(); // preserve original order
        return new Plan(keepList, prune);
    }

    private static DateTime TruncateToHour(DateTime dt) =>
        new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc);

    private static DateTime TruncateToDay(DateTime dt) =>
        new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc);

    // Week buckets keyed by the Sunday-anchored start of the week (UTC).
    private static DateTime TruncateToWeek(DateTime dt)
    {
        var day = TruncateToDay(dt);
        return day.AddDays(-(int)day.DayOfWeek);
    }
}
