using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ActiveVenueFinder.Models;

namespace ActiveVenueFinder.Services;

public static class VenueResolver
{
    public static readonly string[] PredefinedTags = { "Gamba", "Giveaway", "Court" };

    public static readonly Dictionary<string, Vector4> DataCenterColors = new()
    {
        { "Aether", new Vector4(0.5f, 0.8f, 1.0f, 1.0f) },
        { "Primal", new Vector4(1.0f, 0.7f, 0.3f, 1.0f) },
        { "Crystal", new Vector4(0.8f, 0.5f, 1.0f, 1.0f) },
        { "Dynamis", new Vector4(0.4f, 0.9f, 0.4f, 1.0f) },
        { "Chaos", new Vector4(1.0f, 0.4f, 0.4f, 1.0f) },
        { "Light", new Vector4(1.0f, 1.0f, 0.5f, 1.0f) },
        { "Elemental", new Vector4(0.3f, 0.9f, 0.9f, 1.0f) },
        { "Gaia", new Vector4(1.0f, 0.5f, 0.7f, 1.0f) },
        { "Mana", new Vector4(0.6f, 1.0f, 0.6f, 1.0f) },
        { "Meteor", new Vector4(1.0f, 0.85f, 0.3f, 1.0f) },
        { "Materia", new Vector4(0.4f, 0.7f, 1.0f, 1.0f) },
    };

    public static readonly Dictionary<string, string> WorldToDataCenter = new()
    {
        // Aether
        { "Adamantoise", "Aether" }, { "Cactuar", "Aether" }, { "Faerie", "Aether" }, { "Gilgamesh", "Aether" },
        { "Jenova", "Aether" }, { "Midgardsormr", "Aether" }, { "Sargatanas", "Aether" }, { "Siren", "Aether" },
        // Primal
        { "Behemoth", "Primal" }, { "Excalibur", "Primal" }, { "Exodus", "Primal" }, { "Famfrit", "Primal" },
        { "Hyperion", "Primal" }, { "Lamia", "Primal" }, { "Leviathan", "Primal" }, { "Ultros", "Primal" },
        // Crystal
        { "Balmung", "Crystal" }, { "Brynhildr", "Crystal" }, { "Coeurl", "Crystal" }, { "Diabolos", "Crystal" },
        { "Goblin", "Crystal" }, { "Malboro", "Crystal" }, { "Mateus", "Crystal" }, { "Zalera", "Crystal" },
        // Dynamis
        { "Cuchulainn", "Dynamis" }, { "Golem", "Dynamis" }, { "Halicarnassus", "Dynamis" }, { "Kraken", "Dynamis" },
        { "Maduin", "Dynamis" }, { "Marilith", "Dynamis" }, { "Rafflesia", "Dynamis" }, { "Seraph", "Dynamis" },
        // Chaos
        { "Cerberus", "Chaos" }, { "Louisoix", "Chaos" }, { "Moogle", "Chaos" }, { "Omega", "Chaos" },
        { "Phantom", "Chaos" }, { "Ragnarok", "Chaos" }, { "Sagittarius", "Chaos" }, { "Spriggan", "Chaos" },
        // Light
        { "Alpha", "Light" }, { "Lich", "Light" }, { "Odin", "Light" }, { "Phoenix", "Light" },
        { "Raiden", "Light" }, { "Shiva", "Light" }, { "Twintania", "Light" }, { "Zodiark", "Light" },
        // Elemental
        { "Aegis", "Elemental" }, { "Atomos", "Elemental" }, { "Carbuncle", "Elemental" }, { "Garuda", "Elemental" },
        { "Gungnir", "Elemental" }, { "Kujata", "Elemental" }, { "Tonberry", "Elemental" }, { "Typhon", "Elemental" },
        // Gaia
        { "Alexander", "Gaia" }, { "Bahamut", "Gaia" }, { "Durandal", "Gaia" }, { "Fenrir", "Gaia" },
        { "Ifrit", "Gaia" }, { "Ridill", "Gaia" }, { "Tiamat", "Gaia" }, { "Ultima", "Gaia" },
        // Mana
        { "Anima", "Mana" }, { "Asura", "Mana" }, { "Chocobo", "Mana" }, { "Hades", "Mana" },
        { "Ixion", "Mana" }, { "Masamune", "Mana" }, { "Pandaemonium", "Mana" }, { "Titan", "Mana" },
        // Meteor
        { "Belias", "Meteor" }, { "Mandragora", "Meteor" }, { "Ramuh", "Meteor" }, { "Shinryu", "Meteor" },
        { "Unicorn", "Meteor" }, { "Valefor", "Meteor" }, { "Yojimbo", "Meteor" }, { "Zeromus", "Meteor" },
        // Materia
        { "Bismarck", "Materia" }, { "Ravana", "Materia" }, { "Sephirot", "Materia" }, { "Sophia", "Materia" },
        { "Zurvan", "Materia" },
    };

    public static readonly Dictionary<string, string> DataCenterRegions = new()
    {
        { "Aether", "NA" }, { "Primal", "NA" }, { "Crystal", "NA" }, { "Dynamis", "NA" },
        { "Chaos", "EU" }, { "Light", "EU" },
        { "Elemental", "JP" }, { "Gaia", "JP" }, { "Mana", "JP" }, { "Meteor", "JP" },
        { "Materia", "OC" },
    };

    public static bool IsCustomVenue(Venue venue) => int.TryParse(venue.Id, out _);

    public static string GetVenueRegion(Venue venue) =>
        DataCenterRegions.TryGetValue(venue.Location.DataCenter, out var region) ? region : "??";

    public static string BuildLifestreamCommand(VenueLocation loc)
    {
        var districtShort = loc.District.Split(' ')[0];
        var target = loc.Apartment is > 0
            ? $"A{loc.Apartment}"
            : $"P{loc.Plot}";
        return $"/li {loc.World} {districtShort} W{loc.Ward} {target}";
    }

    public static string BuildVenuePageUrl(Venue venue) => $"https://ffxivvenues.com/venue/{venue.Id}";

    public static Venue ApplyOverride(Venue apiVenue, VenueOverride ov)
    {
        var dc = WorldToDataCenter.GetValueOrDefault(ov.World, apiVenue.Location.DataCenter);
        var venue = new Venue
        {
            Id = apiVenue.Id,
            Name = ov.Name,
            Sfw = ov.Sfw,
            Location = new VenueLocation
            {
                DataCenter = dc,
                World = ov.World,
                District = ov.District,
                Ward = ov.Ward,
                Plot = ov.Plot,
                Apartment = ov.Apartment,
                Subdivision = ov.Subdivision,
            },
        };

        if (ov.Schedules.Count > 0)
        {
            ResolveCustomSchedules(venue, ov.TimezoneId, ov.Schedules);
        }
        else
        {
            venue.Schedule = apiVenue.Schedule;
            venue.ScheduleOverrides = apiVenue.ScheduleOverrides;
            venue.Resolution = apiVenue.Resolution;
        }

        return venue;
    }

    public static Venue CustomToVenue(CustomVenue cv)
    {
        var dc = WorldToDataCenter.GetValueOrDefault(cv.World, "");
        var venue = new Venue
        {
            Id = cv.Id.ToString(),
            Name = cv.Name,
            Sfw = cv.Sfw,
            Location = new VenueLocation
            {
                DataCenter = dc,
                World = cv.World,
                District = cv.District,
                Ward = cv.Ward,
                Plot = cv.Plot,
                Apartment = cv.Apartment,
                Subdivision = cv.Subdivision,
            },
        };
        // Materialize all custom schedules into venue.Schedule (for TimelineCalculator expansion).
        foreach (var s in cv.Schedules)
        {
            venue.Schedule.Add(new VenueSchedule
            {
                Day = (int)s.Day,
                Start = new VenueTime { Hour = s.StartHour, Minute = s.StartMinute, TimeZone = cv.TimezoneId },
                End = new VenueTime { Hour = s.EndHour, Minute = s.EndMinute, TimeZone = cv.TimezoneId, NextDay = s.NextDay },
            });
        }
        // Also set Resolution if currently active (for legacy code paths).
        ResolveCustomSchedules(venue, cv.TimezoneId, cv.Schedules);
        return venue;
    }

    public static void ResolveCustomSchedules(Venue venue, string timezoneId, List<CustomVenueSchedule> schedules)
    {
        if (schedules.Count == 0)
            return;

        // timezoneId is intentionally ignored: all schedules run in US Eastern Time.
        try
        {
            var tz = TimelineCalculator.EasternTime;
            var utcNow = DateTimeOffset.UtcNow;
            var localNow = TimeZoneInfo.ConvertTime(utcNow, tz);
            var today = localNow.DateTime.Date;

            foreach (var sched in schedules)
            {
                var daysBack = ((int)localNow.DayOfWeek - (int)sched.Day + 7) % 7;
                var schedDate = today.AddDays(-daysBack);
                var startDt = new DateTime(schedDate.Year, schedDate.Month, schedDate.Day,
                    sched.StartHour, sched.StartMinute, 0);
                var endDate = sched.NextDay ? schedDate.AddDays(1) : schedDate;
                var endDt = new DateTime(endDate.Year, endDate.Month, endDate.Day,
                    sched.EndHour, sched.EndMinute, 0);

                if (localNow.DateTime >= startDt && localNow.DateTime < endDt)
                {
                    var startOffset = new DateTimeOffset(startDt, tz.GetUtcOffset(startDt));
                    var endOffset = new DateTimeOffset(endDt, tz.GetUtcOffset(endDt));
                    venue.Resolution = new VenueResolution
                    {
                        IsNow = true,
                        Start = startOffset.ToString("o"),
                        End = endOffset.ToString("o"),
                        IsWithinWeek = true,
                    };
                    return;
                }
            }
        }
        catch
        {
            // Invalid TZ — silently skip
        }
    }

    public static List<Venue> BuildAll(List<Venue>? apiVenues, Config config)
    {
        var list = new List<Venue>();
        if (apiVenues != null)
        {
            foreach (var v in apiVenues)
            {
                list.Add(config.VenueOverrides.TryGetValue(v.Id, out var ov)
                    ? ApplyOverride(v, ov)
                    : v);
            }
        }
        foreach (var cv in config.CustomVenues)
            list.Add(CustomToVenue(cv));
        return list;
    }
}
