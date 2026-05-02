using System;
using System.Collections.Generic;
using System.Linq;
using ActiveVenueFinder.Models;

namespace ActiveVenueFinder.Services.Tags;

public enum VenueTagSource
{
    Api,
    Local,
    Inferred,
}

// Single ordered tag entry returned by VenueTagService.GetEffective.
public readonly record struct TaggedEntry(string Tag, VenueTagSource Source);

// Owns the effective-tag merge rule (Api > Local > Inferred). Persists Local tag edits and
// surfaces the predefined tag pool used by AddEdit + Settings.
public sealed class VenueTagService
{
    public static readonly IReadOnlyList<string> PredefinedTags = new[] { "Gamba", "Giveaway", "Court" };

    private const int MaxLocalTagLength = 32;
    private const int MaxLocalTagsPerVenue = 32;

    private readonly Config config;
    private readonly Action onConfigChanged;

    public VenueTagService(Config config, Action onConfigChanged)
    {
        this.config = config;
        this.onConfigChanged = onConfigChanged;
    }

    // Returns Api, then Local, then (when enabled) Inferred entries. Earlier sources win on
    // case-insensitive duplicates.
    public IReadOnlyList<TaggedEntry> GetEffective(Venue venue)
    {
        var result = new List<TaggedEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (venue.Tags is { Count: > 0 })
        {
            foreach (var raw in venue.Tags)
            {
                var trimmed = raw?.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (!seen.Add(trimmed!)) continue;
                result.Add(new TaggedEntry(trimmed!, VenueTagSource.Api));
            }
        }

        var key = VenueKey.FromVenue(venue).ToString();
        if (config.LocalTags.TryGetValue(key, out var local))
        {
            foreach (var raw in local)
            {
                var trimmed = raw?.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (!seen.Add(trimmed!)) continue;
                result.Add(new TaggedEntry(trimmed!, VenueTagSource.Local));
            }
        }

        if (config.InferTagsFromDescription && venue.Description is { Count: > 0 })
        {
            var descText = string.Join(" ", venue.Description);
            if (!string.IsNullOrEmpty(descText))
            {
                foreach (var candidate in PredefinedTags.Concat(config.CustomTags))
                {
                    if (string.IsNullOrEmpty(candidate)) continue;
                    if (seen.Contains(candidate)) continue;
                    if (descText.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        seen.Add(candidate);
                        result.Add(new TaggedEntry(candidate, VenueTagSource.Inferred));
                    }
                }
            }
        }

        return result;
    }

    // Convenience: flat list of just the tag strings, useful for filter prefix matching.
    public IReadOnlyList<string> GetEffectiveTagStrings(Venue venue) =>
        GetEffective(venue).Select(e => e.Tag).ToArray();

    // Case-insensitive prefix check across all effective tag sources.
    public bool MatchesTagPrefix(Venue venue, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return true;
        foreach (var entry in GetEffective(venue))
        {
            if (entry.Tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Returns the user-editable Local tags for a venue (never API or Inferred).
    public List<string> GetLocal(VenueKey key)
    {
        return config.LocalTags.TryGetValue(key.ToString(), out var list) ? list : new List<string>();
    }

    // Adds a local tag to the venue, deduplicated case-insensitively against API tags too.
    // No-ops on empty input or duplicates.
    public void AddLocal(VenueKey key, string tag, IReadOnlyList<string>? apiTagsForDedupe = null)
    {
        var trimmed = tag?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;
        if (trimmed!.Length > MaxLocalTagLength) trimmed = trimmed.Substring(0, MaxLocalTagLength);

        if (apiTagsForDedupe != null)
        {
            foreach (var api in apiTagsForDedupe)
            {
                if (string.Equals(api, trimmed, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        var keyStr = key.ToString();
        if (!config.LocalTags.TryGetValue(keyStr, out var list))
        {
            list = new List<string>();
            config.LocalTags[keyStr] = list;
        }
        if (list.Count >= MaxLocalTagsPerVenue) return;
        if (list.Any(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase))) return;

        list.Add(trimmed!);
        config.Save();
        onConfigChanged();
    }

    // Removes a local tag (case-insensitive). No-op if not present.
    public void RemoveLocal(VenueKey key, string tag)
    {
        var keyStr = key.ToString();
        if (!config.LocalTags.TryGetValue(keyStr, out var list)) return;
        var idx = list.FindIndex(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;
        list.RemoveAt(idx);
        if (list.Count == 0) config.LocalTags.Remove(keyStr);
        config.Save();
        onConfigChanged();
    }

    // Bulk replace: used by AddEditVenueWindow to commit the edited tag set in one shot.
    public void SetLocal(VenueKey key, IEnumerable<string> tags, IReadOnlyList<string>? apiTagsForDedupe = null)
    {
        var keyStr = key.ToString();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (apiTagsForDedupe != null)
        {
            foreach (var api in apiTagsForDedupe) seen.Add(api);
        }
        var clean = new List<string>();
        foreach (var raw in tags)
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            if (trimmed!.Length > MaxLocalTagLength) trimmed = trimmed.Substring(0, MaxLocalTagLength);
            if (!seen.Add(trimmed!)) continue;
            if (clean.Count >= MaxLocalTagsPerVenue) break;
            clean.Add(trimmed!);
        }

        if (clean.Count == 0)
            config.LocalTags.Remove(keyStr);
        else
            config.LocalTags[keyStr] = clean;
        config.Save();
        onConfigChanged();
    }
}
