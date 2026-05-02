using System;

namespace ActiveVenueFinder.Services.Filtering;

public enum WorldFilterKind
{
    None,
    DataCenter,
    World,
}

// Parsed search bar state. Currently supports a name fragment OR a "T:tag" prefix query.
public sealed record VenueSearchQuery(
    string? NameFragment,
    string? TagPrefix,
    WorldFilterKind WorldKind,
    string? WorldOrDc,
    string? District)
{
    // Translates the raw search input + persisted world/district filters into a structured query.
    public static VenueSearchQuery Parse(string searchText, string filterWorld, string filterDistrict)
    {
        string? nameFrag = null;
        string? tagPrefix = null;

        if (!string.IsNullOrEmpty(searchText))
        {
            if (searchText.StartsWith("T:", StringComparison.OrdinalIgnoreCase))
            {
                var tag = searchText.Substring(2).Trim();
                if (!string.IsNullOrEmpty(tag)) tagPrefix = tag;
            }
            else
            {
                nameFrag = searchText;
            }
        }

        WorldFilterKind kind = WorldFilterKind.None;
        string? worldOrDc = null;
        if (!string.IsNullOrEmpty(filterWorld))
        {
            if (filterWorld.StartsWith("DC:", StringComparison.Ordinal))
            {
                kind = WorldFilterKind.DataCenter;
                worldOrDc = filterWorld.Substring(3);
            }
            else if (filterWorld.StartsWith("World:", StringComparison.Ordinal))
            {
                kind = WorldFilterKind.World;
                worldOrDc = filterWorld.Substring(6);
            }
        }

        return new VenueSearchQuery(
            nameFrag,
            tagPrefix,
            kind,
            worldOrDc,
            string.IsNullOrEmpty(filterDistrict) ? null : filterDistrict);
    }
}
