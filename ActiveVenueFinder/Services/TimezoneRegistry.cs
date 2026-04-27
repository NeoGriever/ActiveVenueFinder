using System;
using System.Collections.Generic;
using System.Linq;

namespace ActiveVenueFinder.Services;

public sealed class TimezoneEntry
{
    public string Id { get; set; } = "";
    public string DisplayLabel { get; set; } = "";
    public string ShortLabel { get; set; } = "";
    public string Region { get; set; } = "";
    public TimeZoneInfo TimeZone { get; set; } = null!;
}

public static class TimezoneRegistry
{
    // (IanaId, WindowsId, ShortLabel, Region) — try IANA first, fall back to Windows id.
    private static readonly (string Iana, string Windows, string Short, string Region)[] CuratedZones =
    {
        // UTC
        ("UTC", "UTC", "UTC", "UTC"),

        // Americas
        ("Pacific/Honolulu",     "Hawaiian Standard Time",        "Honolulu",        "Americas"),
        ("America/Anchorage",    "Alaskan Standard Time",         "Anchorage",       "Americas"),
        ("America/Los_Angeles",  "Pacific Standard Time",         "Los Angeles (PT)", "Americas"),
        ("America/Denver",       "Mountain Standard Time",        "Denver (MT)",     "Americas"),
        ("America/Phoenix",      "US Mountain Standard Time",     "Phoenix (MST)",   "Americas"),
        ("America/Chicago",      "Central Standard Time",         "Chicago (CT)",    "Americas"),
        ("America/New_York",     "Eastern Standard Time",         "New York (ET)",   "Americas"),
        ("America/Toronto",      "Eastern Standard Time",         "Toronto",         "Americas"),
        ("America/Halifax",      "Atlantic Standard Time",        "Halifax",         "Americas"),
        ("America/Mexico_City",  "Central Standard Time (Mexico)","Mexico City",     "Americas"),
        ("America/Bogota",       "SA Pacific Standard Time",      "Bogota",          "Americas"),
        ("America/Sao_Paulo",    "E. South America Standard Time","Sao Paulo",       "Americas"),
        ("America/Buenos_Aires", "Argentina Standard Time",       "Buenos Aires",    "Americas"),

        // Europe
        ("Europe/London",     "GMT Standard Time",        "London",     "Europe"),
        ("Europe/Dublin",     "GMT Standard Time",        "Dublin",     "Europe"),
        ("Europe/Lisbon",     "GMT Standard Time",        "Lisbon",     "Europe"),
        ("Europe/Madrid",     "Romance Standard Time",    "Madrid",     "Europe"),
        ("Europe/Paris",      "Romance Standard Time",    "Paris",      "Europe"),
        ("Europe/Berlin",     "W. Europe Standard Time",  "Berlin",     "Europe"),
        ("Europe/Amsterdam",  "W. Europe Standard Time",  "Amsterdam",  "Europe"),
        ("Europe/Brussels",   "Romance Standard Time",    "Brussels",   "Europe"),
        ("Europe/Zurich",     "W. Europe Standard Time",  "Zurich",     "Europe"),
        ("Europe/Rome",       "W. Europe Standard Time",  "Rome",       "Europe"),
        ("Europe/Vienna",     "W. Europe Standard Time",  "Vienna",     "Europe"),
        ("Europe/Warsaw",     "Central European Standard Time", "Warsaw", "Europe"),
        ("Europe/Stockholm",  "W. Europe Standard Time",  "Stockholm",  "Europe"),
        ("Europe/Helsinki",   "FLE Standard Time",        "Helsinki",   "Europe"),
        ("Europe/Athens",     "GTB Standard Time",        "Athens",     "Europe"),
        ("Europe/Istanbul",   "Turkey Standard Time",     "Istanbul",   "Europe"),
        ("Europe/Moscow",     "Russian Standard Time",    "Moscow",     "Europe"),

        // Africa
        ("Africa/Cairo",        "Egypt Standard Time",         "Cairo",        "Africa"),
        ("Africa/Johannesburg", "South Africa Standard Time",  "Johannesburg", "Africa"),
        ("Africa/Lagos",        "W. Central Africa Standard Time", "Lagos",    "Africa"),
        ("Africa/Nairobi",      "E. Africa Standard Time",     "Nairobi",      "Africa"),

        // Asia
        ("Asia/Dubai",     "Arabian Standard Time",      "Dubai",         "Asia"),
        ("Asia/Tehran",    "Iran Standard Time",         "Tehran",        "Asia"),
        ("Asia/Karachi",   "Pakistan Standard Time",     "Karachi",       "Asia"),
        ("Asia/Kolkata",   "India Standard Time",        "Kolkata (IST)", "Asia"),
        ("Asia/Bangkok",   "SE Asia Standard Time",      "Bangkok",       "Asia"),
        ("Asia/Singapore", "Singapore Standard Time",    "Singapore",     "Asia"),
        ("Asia/Hong_Kong", "China Standard Time",        "Hong Kong",     "Asia"),
        ("Asia/Shanghai",  "China Standard Time",        "Shanghai",      "Asia"),
        ("Asia/Taipei",    "Taipei Standard Time",       "Taipei",        "Asia"),
        ("Asia/Seoul",     "Korea Standard Time",        "Seoul",         "Asia"),
        ("Asia/Tokyo",     "Tokyo Standard Time",        "Tokyo (JST)",   "Asia"),
        ("Asia/Manila",    "Singapore Standard Time",    "Manila",        "Asia"),
        ("Asia/Jakarta",   "SE Asia Standard Time",      "Jakarta",       "Asia"),

        // Pacific
        ("Australia/Perth",     "W. Australia Standard Time", "Perth",     "Pacific"),
        ("Australia/Adelaide",  "Cen. Australia Standard Time","Adelaide", "Pacific"),
        ("Australia/Sydney",    "AUS Eastern Standard Time",  "Sydney",    "Pacific"),
        ("Australia/Brisbane",  "E. Australia Standard Time", "Brisbane",  "Pacific"),
        ("Pacific/Auckland",    "New Zealand Standard Time",  "Auckland",  "Pacific"),
        ("Pacific/Fiji",        "Fiji Standard Time",         "Fiji",      "Pacific"),
    };

    private static List<TimezoneEntry>? cached;
    private static long cacheTimestamp;

    public static IReadOnlyList<TimezoneEntry> All
    {
        get
        {
            var now = Environment.TickCount64;
            if (cached == null || now - cacheTimestamp > 60_000)
            {
                cached = Build();
                cacheTimestamp = now;
            }
            return cached;
        }
    }

    private static List<TimezoneEntry> Build()
    {
        var result = new List<TimezoneEntry>();
        var seenIds = new HashSet<string>();

        foreach (var (iana, win, shortLabel, region) in CuratedZones)
        {
            var tz = TryFind(iana) ?? TryFind(win);
            if (tz == null) continue;
            if (!seenIds.Add(tz.Id)) continue; // avoid showing same TZ twice if multiple aliases collapse

            var offset = tz.GetUtcOffset(DateTimeOffset.UtcNow);
            var sign = offset.Ticks >= 0 ? "+" : "-";
            var abs = offset.Duration();
            var offsetLabel = abs.Minutes == 0
                ? $"UTC{sign}{abs.Hours}"
                : $"UTC{sign}{abs.Hours}:{abs.Minutes:D2}";

            result.Add(new TimezoneEntry
            {
                Id = tz.Id,
                ShortLabel = shortLabel,
                Region = region,
                TimeZone = tz,
                DisplayLabel = $"{shortLabel} ({offsetLabel})",
            });
        }

        // Hard fallback: if curated mapping yielded nothing (unusual environment),
        // expose every system zone unfiltered so the user can still pick something.
        if (result.Count == 0)
        {
            foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
            {
                var offset = tz.GetUtcOffset(DateTimeOffset.UtcNow);
                var sign = offset.Ticks >= 0 ? "+" : "-";
                var abs = offset.Duration();
                var offsetLabel = abs.Minutes == 0
                    ? $"UTC{sign}{abs.Hours}"
                    : $"UTC{sign}{abs.Hours}:{abs.Minutes:D2}";
                result.Add(new TimezoneEntry
                {
                    Id = tz.Id,
                    ShortLabel = tz.Id,
                    Region = "All",
                    TimeZone = tz,
                    DisplayLabel = $"{tz.Id} ({offsetLabel})",
                });
            }
        }

        return result;
    }

    private static TimeZoneInfo? TryFind(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return null; }
    }

    public static TimezoneEntry? FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return All.FirstOrDefault(e => e.Id == id);
    }

    public static string FormatLocal(TimeZoneInfo tz)
    {
        var entry = All.FirstOrDefault(e => e.TimeZone.Id == tz.Id);
        if (entry != null) return entry.DisplayLabel;
        var offset = tz.GetUtcOffset(DateTimeOffset.UtcNow);
        var sign = offset.Ticks >= 0 ? "+" : "-";
        var abs = offset.Duration();
        return $"{tz.Id} (UTC{sign}{abs.Hours}{(abs.Minutes == 0 ? "" : $":{abs.Minutes:D2}")})";
    }
}
