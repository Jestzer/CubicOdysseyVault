namespace CubicOdysseyVault.Core.SaveContent;

// Per-filename mapping of TLV top-level tag numbers to friendly names. Anchored
// to confirmed observations from the user's actual save data. Unknown filenames
// or unknown tags return null and the UI shows the raw tag number — annotations
// grow incrementally as more fields get reverse-engineered.
public static class KnownTagAnnotations
{
    private static readonly Dictionary<string, IReadOnlyDictionary<int, string>> Tags =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Slot-level metadata: tags 5-10 round-trip the local-time timestamp
            // of the save (verified against file mtime: 2026-05-05 22:30 →
            // tag5=5 (month), tag6=5 (day), tag7=2026 (year), tag8=22 (hour),
            // tag9=30 (minute), tag10=0 (second)).
            ["93_meta.sav"] = new Dictionary<int, string>
            {
                [5] = "Month",
                [6] = "Day",
                [7] = "Year",
                [8] = "Hour",
                [9] = "Minute",
                [10] = "Second",
            },
            // Account-level shared files — top tags wrap inner lists; the
            // names here describe the wrapper, not what's inside.
            ["93_blueprints.sav"] = new Dictionary<int, string>
            {
                [1] = "BlueprintCount",
                [2] = "BlueprintList",
            },
            ["93_servers.sav"] = new Dictionary<int, string>
            {
                [1] = "ServerList",
            },
            ["93_stats.sav"] = new Dictionary<int, string>
            {
                [1] = "PlaytimeRelated",
            },
            ["93_quests.sav"] = new Dictionary<int, string>
            {
                [1] = "QuestList",
            },
            ["93_economy.sav"] = new Dictionary<int, string>
            {
                [1] = "Currencies",
            },
        };

    public static string? Lookup(string fileName, int tag)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        if (!Tags.TryGetValue(fileName, out var fileTags)) return null;
        return fileTags.TryGetValue(tag, out var name) ? name : null;
    }
}
