using System;
using System.Collections.Generic;
using System.Linq;
using ActiveVenueFinder.Models;

namespace ActiveVenueFinder.Services;

public readonly struct TimeSlot
{
    public readonly DateTimeOffset StartUtc;
    public readonly DateTimeOffset EndUtc;
    public readonly bool IsActive;
    public readonly bool AlwaysOpen;

    public TimeSlot(DateTimeOffset startUtc, DateTimeOffset endUtc, bool isActive, bool alwaysOpen)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
        IsActive = isActive;
        AlwaysOpen = alwaysOpen;
    }

    public TimeSpan Duration => EndUtc - StartUtc;
}

public static class TimelineCalculator
{
    private const double AlwaysOpenThresholdHours = 18.0;
    private const double MergeGapThresholdMinutes = 30.0;

    /// <summary>
    /// All venue schedules are interpreted in US Eastern Time, regardless of the timeZone string
    /// reported by the source data. The system zoneinfo database may expose this under a few
    /// different IDs ("America/New_York" on Linux/macOS, "Eastern Standard Time" on Windows);
    /// EasternTime resolves whichever is available, falling back to a fixed UTC-5 offset.
    /// </summary>
    public static readonly TimeZoneInfo EasternTime = ResolveEasternTime();

    private static TimeZoneInfo ResolveEasternTime()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time", "EST5EDT", "US/Eastern" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* try next */ }
        }
        return TimeZoneInfo.CreateCustomTimeZone("EST", TimeSpan.FromHours(-5), "Eastern (fallback, no DST)", "EST");
    }

    /// <summary>
    /// Returns all opening slots that intersect [windowStartUtc, windowEndUtc].
    /// Sources priority: Custom/Override schedules → ScheduleOverrides → Schedule (recurring).
    /// Slots within MergeGapThresholdMinutes of each other are merged.
    /// </summary>
    public static List<TimeSlot> GetSlotsInWindow(
        Venue venue,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc)
    {
        var slots = new List<TimeSlot>();
        var nowUtc = DateTimeOffset.UtcNow;

        // 1. ScheduleOverrides (concrete ISO timestamps from API)
        if (venue.ScheduleOverrides is { Count: > 0 })
        {
            foreach (var ov in venue.ScheduleOverrides)
            {
                if (ov == null || !ov.Open) continue;
                if (string.IsNullOrEmpty(ov.Start) || string.IsNullOrEmpty(ov.End)) continue;
                if (!TryParseIso(ov.Start, out var s) || !TryParseIso(ov.End, out var e)) continue;
                if (e <= windowStartUtc || s >= windowEndUtc) continue;

                var isActive = nowUtc >= s && nowUtc < e;
                var alwaysOpen = (e - s).TotalHours > AlwaysOpenThresholdHours;
                slots.Add(new TimeSlot(s, e, isActive, alwaysOpen));
            }
        }

        // 2. Resolution (precomputed by API or by ResolveCustomSchedules) — only if not already covered
        if (slots.Count == 0 && venue.Resolution != null
            && !string.IsNullOrEmpty(venue.Resolution.Start)
            && !string.IsNullOrEmpty(venue.Resolution.End))
        {
            if (TryParseIso(venue.Resolution.Start, out var s) && TryParseIso(venue.Resolution.End, out var e))
            {
                if (e > windowStartUtc && s < windowEndUtc)
                {
                    var isActive = venue.Resolution.IsNow || (nowUtc >= s && nowUtc < e);
                    var alwaysOpen = (e - s).TotalHours > AlwaysOpenThresholdHours;
                    slots.Add(new TimeSlot(s, e, isActive, alwaysOpen));
                }
            }
        }

        // 3. Recurring Schedule (weekly), expanded into the window.
        if (venue.Schedule is { Count: > 0 })
        {
            ExpandRecurringSchedule(venue, slots, windowStartUtc, windowEndUtc, nowUtc);
        }

        return MergeSlots(slots);
    }

    private static void ExpandRecurringSchedule(
        Venue venue,
        List<TimeSlot> slots,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        DateTimeOffset nowUtc)
    {
        // Walk day by day across the window. All schedules are interpreted in US Eastern Time
        // (see EasternTime); the timeZone string from the source is intentionally ignored to
        // guarantee consistent timing regardless of upstream data quality.
        var existingStarts = new HashSet<DateTimeOffset>(slots.Select(s => s.StartUtc));

        var currentDay = windowStartUtc.UtcDateTime.Date.AddDays(-1); // back one day for safety
        var lastDay = windowEndUtc.UtcDateTime.Date.AddDays(1);
        var tz = EasternTime;

        while (currentDay <= lastDay)
        {
            foreach (var sch in venue.Schedule)
            {
                if (sch?.Start == null || sch.End == null) continue;

                DateTimeOffset tzMidnight;
                try
                {
                    tzMidnight = TimeZoneInfo.ConvertTime(new DateTimeOffset(currentDay, TimeSpan.Zero), tz);
                }
                catch { continue; }

                // API uses Mon=0..Sun=6, .NET DayOfWeek uses Sun=0..Sat=6.
                if ((((int)tzMidnight.DayOfWeek + 6) % 7) != sch.Day)
                    continue;

                var localDate = tzMidnight.DateTime.Date;
                DateTime startLocal, endLocal;
                try
                {
                    startLocal = new DateTime(localDate.Year, localDate.Month, localDate.Day,
                        Math.Clamp(sch.Start.Hour, 0, 23),
                        Math.Clamp(sch.Start.Minute, 0, 59),
                        0, DateTimeKind.Unspecified);
                    var endDate = sch.End.NextDay ? localDate.AddDays(1) : localDate;
                    endLocal = new DateTime(endDate.Year, endDate.Month, endDate.Day,
                        Math.Clamp(sch.End.Hour, 0, 23),
                        Math.Clamp(sch.End.Minute, 0, 59),
                        0, DateTimeKind.Unspecified);
                }
                catch { continue; }

                DateTimeOffset startUtc, endUtc;
                try
                {
                    var startOffset = tz.GetUtcOffset(startLocal);
                    var endOffset = tz.GetUtcOffset(endLocal);
                    startUtc = new DateTimeOffset(startLocal, startOffset).ToUniversalTime();
                    endUtc = new DateTimeOffset(endLocal, endOffset).ToUniversalTime();
                }
                catch { continue; }

                if (endUtc <= windowStartUtc || startUtc >= windowEndUtc) continue;
                if (existingStarts.Contains(startUtc)) continue;

                var isActive = nowUtc >= startUtc && nowUtc < endUtc;
                var alwaysOpen = (endUtc - startUtc).TotalHours > AlwaysOpenThresholdHours;
                slots.Add(new TimeSlot(startUtc, endUtc, isActive, alwaysOpen));
                existingStarts.Add(startUtc);
            }
            currentDay = currentDay.AddDays(1);
        }
    }

    private static bool TryParseIso(string iso, out DateTimeOffset result)
    {
        if (DateTimeOffset.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out result))
            return true;
        result = default;
        return false;
    }

    /// <summary>
    /// Sort by start, then merge slots whose gap is less than 30 minutes.
    /// </summary>
    private static List<TimeSlot> MergeSlots(List<TimeSlot> slots)
    {
        if (slots.Count <= 1) return slots;

        slots.Sort((a, b) => a.StartUtc.CompareTo(b.StartUtc));
        var merged = new List<TimeSlot> { slots[0] };
        for (var i = 1; i < slots.Count; i++)
        {
            var prev = merged[^1];
            var cur = slots[i];
            var gap = (cur.StartUtc - prev.EndUtc).TotalMinutes;
            if (gap <= MergeGapThresholdMinutes)
            {
                var newEnd = cur.EndUtc > prev.EndUtc ? cur.EndUtc : prev.EndUtc;
                merged[^1] = new TimeSlot(
                    prev.StartUtc,
                    newEnd,
                    prev.IsActive || cur.IsActive,
                    prev.AlwaysOpen || cur.AlwaysOpen);
            }
            else
            {
                merged.Add(cur);
            }
        }
        return merged;
    }

    public static TimeSlot? GetActiveSlot(List<TimeSlot> slots)
    {
        foreach (var s in slots)
            if (s.IsActive) return s;
        return null;
    }

    public static TimeSlot? GetNextSlot(List<TimeSlot> slots, DateTimeOffset afterUtc)
    {
        TimeSlot? best = null;
        foreach (var s in slots)
        {
            if (s.StartUtc <= afterUtc) continue;
            if (best == null || s.StartUtc < best.Value.StartUtc)
                best = s;
        }
        return best;
    }

    public static bool IsActiveNow(List<TimeSlot> slots)
    {
        return GetActiveSlot(slots).HasValue;
    }

    public static (TimeSpan remaining, bool alwaysOpen) GetRemainingFromActive(List<TimeSlot> slots)
    {
        var active = GetActiveSlot(slots);
        if (!active.HasValue) return (TimeSpan.Zero, false);
        var rem = active.Value.EndUtc - DateTimeOffset.UtcNow;
        if (rem <= TimeSpan.Zero) return (TimeSpan.Zero, false);
        return (rem, active.Value.AlwaysOpen);
    }
}
