using System;
using System.Collections.Generic;
using ActiveVenueFinder.Models;

namespace ActiveVenueFinder.Services.Schedule;

public enum ScheduleViewIntent
{
    Table,
    Timeline,
    Info,
}

// Single source of truth for opening-slot resolution. All UI consumers (table, timeline, popout, info)
// must call GetSlots so the same precedence rule applies everywhere.
//
// Precedence:
//   1. ScheduleOverrides (concrete API exceptions for specific dates).
//   2. Recurring weekly schedule, exactly one of (in this order):
//        a. config.VenueOverrides[key].Schedules  (local override on an API venue)
//        b. CustomVenue.Schedules                  (custom venue)
//        c. venue.Schedule                         (API recurring)
//      Times are interpreted in US Eastern Time. The per-slot TimeZone string from the API is
//      intentionally ignored.
//   3. venue.Resolution: never authoritative; only flags an "active right now" slot if Steps 1-2
//      produced none covering UtcNow.
//   4. Sort by start, merge slots with gap < 30 min.
//
// VenueResolver.BuildAll already applies (1)/(2a)/(2b) by collapsing api+override+custom into a
// flat Venue list whose Schedule/ScheduleOverrides/Resolution fields reflect the chosen source.
// This resolver therefore delegates expansion to TimelineCalculator and exposes view-shaped helpers.
public static class VenueScheduleResolver
{
    public const double TimelineWindowHours = 48.0;

    // Returns slots that intersect [startUtc, endUtc]. Always UTC; UI projects to display TZ.
    public static List<TimeSlot> GetSlots(Venue venue, DateTimeOffset startUtc, DateTimeOffset endUtc)
        => TimelineCalculator.GetSlotsInWindow(venue, startUtc, endUtc);

    // Convenience for Info view: 7-day window centered on now (-1d ... +6d).
    public static List<TimeSlot> GetSlotsForWeek(Venue venue, DateTimeOffset nowUtc)
    {
        var start = nowUtc - TimeSpan.FromDays(1);
        var end = nowUtc + TimeSpan.FromDays(7);
        return GetSlots(venue, start, end);
    }

    // Buckets slots into 7 weekday lists keyed by their start in the supplied display timezone.
    // Index 0 = Monday (matches API convention used by the rest of the project).
    public static List<List<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)>> GroupByDisplayDay(
        IEnumerable<TimeSlot> slots,
        TimeZoneInfo displayTz)
    {
        var result = new List<List<(DateTimeOffset, DateTimeOffset)>>(7);
        for (var i = 0; i < 7; i++) result.Add(new List<(DateTimeOffset, DateTimeOffset)>());

        var seen = new HashSet<(int, long)>();
        foreach (var slot in slots)
        {
            var startLocal = TimeZoneInfo.ConvertTime(slot.StartUtc, displayTz);
            var dayIndex = ((int)startLocal.DayOfWeek + 6) % 7; // Mon=0..Sun=6
            // Dedupe across weeks for week-grid rendering (one slot per weekday weekly recurrence).
            var minuteOfDay = startLocal.Hour * 60L + startLocal.Minute;
            var dedupeKey = ((int)slot.Duration.TotalMinutes, minuteOfDay + dayIndex * 24 * 60);
            if (!seen.Add(dedupeKey)) continue;
            result[dayIndex].Add((slot.StartUtc, slot.EndUtc));
        }

        for (var i = 0; i < 7; i++)
        {
            result[i].Sort((a, b) => a.Item1.CompareTo(b.Item1));
        }
        return result;
    }
}
