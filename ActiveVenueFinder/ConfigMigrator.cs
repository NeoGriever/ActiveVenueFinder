using System.Collections.Generic;
using ActiveVenueFinder.Models;

namespace ActiveVenueFinder;

// Idempotent v1 -> v2 config migration. Runs once on plugin startup right after the config is loaded.
public static class ConfigMigrator
{
    public static void Run(Config config)
    {
        if (config.Version >= 2)
            return;

        // Move legacy override-level tags into the new per-venue LocalTags dict.
        foreach (var (id, ov) in config.VenueOverrides)
        {
            if (ov.Tags is { Count: > 0 })
            {
                var key = VenueKey.Api(id).ToString();
                MergeIntoLocalTags(config.LocalTags, key, ov.Tags);
                ov.Tags.Clear();
            }
            ov.TimezoneId = "";
        }

        foreach (var cv in config.CustomVenues)
        {
            if (cv.Tags is { Count: > 0 })
            {
                var key = VenueKey.Custom(cv.Id).ToString();
                MergeIntoLocalTags(config.LocalTags, key, cv.Tags);
                cv.Tags.Clear();
            }
            cv.TimezoneId = "";
        }

        // Force-migrate to the new default. Power users can re-pick LifestreamGoto in Settings.
        config.DoubleClickAction = DoubleClickAction.OpenInfo;

        // Inference is opt-in going forward.
        config.InferTagsFromDescription = false;

        config.Version = 2;
        config.Save();

        Plugin.Log.Info("Config migrated to v2.");
    }

    private static void MergeIntoLocalTags(Dictionary<string, List<string>> bag, string key, IEnumerable<string> tags)
    {
        if (!bag.TryGetValue(key, out var list))
        {
            list = new List<string>();
            bag[key] = list;
        }
        foreach (var t in tags)
        {
            var trimmed = t?.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            if (list.Contains(trimmed!)) continue;
            list.Add(trimmed!);
        }
    }
}
