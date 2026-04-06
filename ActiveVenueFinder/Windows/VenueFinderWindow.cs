using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using ActiveVenueFinder.Models;

namespace ActiveVenueFinder.Windows;

public sealed class VenueFinderWindow : Window, IDisposable
{
    internal const string ApiUrl = "https://api.ffxivvenues.com/v1.0/venue";

    internal static readonly Dictionary<string, Vector4> DataCenterColors = new()
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

    internal static readonly Dictionary<string, string> DataCenterRegions = new()
    {
        { "Aether", "NA" }, { "Primal", "NA" }, { "Crystal", "NA" }, { "Dynamis", "NA" },
        { "Chaos", "EU" }, { "Light", "EU" },
        { "Elemental", "JP" }, { "Gaia", "JP" }, { "Mana", "JP" }, { "Meteor", "JP" },
        { "Materia", "OC" },
    };

    internal static readonly string[] PredefinedTags = { "Gamba", "Giveaway", "Court" };

    private static readonly TimeZoneInfo EstZone = GetEstZone();

    private static TimeZoneInfo GetEstZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch (TimeZoneNotFoundException) { }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
        catch (TimeZoneNotFoundException) { }
        return TimeZoneInfo.CreateCustomTimeZone("EST", TimeSpan.FromHours(-5), "EST", "EST");
    }

    internal static readonly Dictionary<string, string> WorldToDataCenter = new()
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

    private readonly IPlayerState playerState;
    private readonly Config config;
    private readonly HttpClient httpClient = new();
    private readonly AddEditVenueWindow addEditWindow;

    internal PopoutWindow? PopoutWindow { get; set; }

    private List<Venue>? venues;
    private bool isLoading;
    private string? errorMessage;
    private string? pendingBlacklistId;

    // Sort state
    private int sortColumnIndex = -1;
    private ImGuiSortDirection sortDirection = ImGuiSortDirection.None;

    // Context menu
    private Venue? contextMenuVenue;
    private bool openBlacklistConfirm;

    private TimeZoneInfo displayTimeZone = TimeZoneInfo.Local;
    private readonly List<TimeZoneInfo> allTimeZones;
    private int selectedTimezoneIndex;

    // Display cache
    private List<VenueRow>? cachedRows;
    private long lastCacheMs;
    private bool cacheInvalidated = true;
    private const long CacheIntervalMs = 3000;
    private List<Venue>? cachedVenuesRef;
    private int cachedCustomVenueCount = -1;
    private string? lastKnownPlayerRegion;
    private string searchText = "";
    private string cachedSearchText = "";
    private int lookaheadHours;
    private int cachedLookaheadHours;

    private sealed class VenueRow
    {
        public Venue Venue = null!;
        public Vector4 TextColor;
        public Vector4 BgColor;
        public string Region = "";
        public string TimeDisplay = "";
        public string RemainingDisplay = "";
        public bool IsFavorite;
        public bool IsCustom;
        public string FavId = "";
        public string GoId = "";
        public string WardStr = "";
        public string PlotStr = "";
        public float TimelineStartFrac;
        public float TimelineEndFrac;
        public bool HasTimelineBar;
        public bool IsApartment;
        public bool IsOtherContinent;
        public HashSet<string> Tags = new();
    }

    public VenueFinderWindow(Config config, IPlayerState playerState, AddEditVenueWindow addEditWindow)
        : base("Active Venue Finder###ActiveVenueFinder")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(250, 150),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(1820, 500);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.config = config;
        this.playerState = playerState;
        this.addEditWindow = addEditWindow;
        this.addEditWindow.OnSaved = () => cacheInvalidated = true;
        allTimeZones = TimeZoneInfo.GetSystemTimeZones().ToList();

        if (!string.IsNullOrEmpty(config.SelectedTimezoneId))
        {
            try
            {
                displayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(config.SelectedTimezoneId);
            }
            catch
            {
                displayTimeZone = TimeZoneInfo.Local;
            }
        }

        selectedTimezoneIndex = 0;
        for (var i = 0; i < allTimeZones.Count; i++)
        {
            if (allTimeZones[i].Id == displayTimeZone.Id && !string.IsNullOrEmpty(config.SelectedTimezoneId))
            {
                selectedTimezoneIndex = i + 1;
                break;
            }
        }
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    public override void OnOpen()
    {
        FetchVenues();
    }

    private void FetchVenues()
    {
        isLoading = true;
        errorMessage = null;
        venues = null;
        cachedRows = null;

        Task.Run(async () =>
        {
            try
            {
                var json = await httpClient.GetStringAsync(ApiUrl);
                var result = JsonSerializer.Deserialize<List<Venue>>(json);
                venues = result ?? new List<Venue>();
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Plugin.Log.Error($"Failed to fetch venues: {ex}");
            }
            finally
            {
                isLoading = false;
            }
        });
    }

    internal static bool IsVenueActiveNow(Venue venue)
    {
        if (venue.ScheduleOverrides.Count > 0)
            return venue.ScheduleOverrides.Any(o => o.IsNow && o.Open);

        return venue.Resolution?.IsNow ?? false;
    }

    internal static (TimeSpan remaining, bool alwaysOpen) GetRemainingOpenInfo(Venue venue)
    {
        string? startIso = null;
        string? endIso = null;

        if (venue.ScheduleOverrides.Count > 0)
        {
            var active = venue.ScheduleOverrides.FirstOrDefault(o => o.IsNow && o.Open);
            startIso = active?.Start;
            endIso = active?.End;
        }
        else if (venue.Resolution is { IsNow: true })
        {
            startIso = venue.Resolution.Start;
            endIso = venue.Resolution.End;
        }

        if (string.IsNullOrEmpty(endIso))
            return (TimeSpan.Zero, false);

        try
        {
            var end = DateTimeOffset.Parse(endIso);
            var remaining = end - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return (TimeSpan.Zero, false);

            if (!string.IsNullOrEmpty(startIso))
            {
                var start = DateTimeOffset.Parse(startIso);
                var totalDuration = end - start;
                if (totalDuration.TotalHours > 18)
                    return (remaining, true);
            }

            return (remaining, false);
        }
        catch
        {
            return (TimeSpan.Zero, false);
        }
    }

    private static TimeSpan GetSortableRemainingTime(Venue venue)
    {
        var (remaining, alwaysOpen) = GetRemainingOpenInfo(venue);
        if (alwaysOpen) return TimeSpan.FromMinutes(1);
        if (remaining > TimeSpan.Zero) return remaining;

        var closedSince = GetClosedSinceDuration(venue);
        if (closedSince == TimeSpan.MaxValue)
            return TimeSpan.MinValue;
        return -closedSince;
    }

    private static TimeSpan GetClosedSinceDuration(Venue venue)
    {
        if (venue.ScheduleOverrides.Count > 0)
        {
            DateTimeOffset? latest = null;
            foreach (var o in venue.ScheduleOverrides)
            {
                if (!string.IsNullOrEmpty(o.End))
                {
                    try
                    {
                        var end = DateTimeOffset.Parse(o.End);
                        if (latest == null || end > latest)
                            latest = end;
                    }
                    catch { }
                }
            }
            if (latest != null)
            {
                var since = DateTimeOffset.UtcNow - latest.Value;
                return since > TimeSpan.Zero ? since : TimeSpan.MaxValue;
            }
        }

        string? endIso = null;
        if (venue.Resolution != null && !string.IsNullOrEmpty(venue.Resolution.End))
            endIso = venue.Resolution.End;

        if (string.IsNullOrEmpty(endIso))
            return TimeSpan.MaxValue;

        try
        {
            var endDt = DateTimeOffset.Parse(endIso);
            var since = DateTimeOffset.UtcNow - endDt;
            return since > TimeSpan.Zero ? since : TimeSpan.MaxValue;
        }
        catch
        {
            return TimeSpan.MaxValue;
        }
    }

    private static TimeSpan GetTimeUntilOpening(Venue venue)
    {
        if (venue.Resolution is { IsNow: false } && !string.IsNullOrEmpty(venue.Resolution.Start))
        {
            try
            {
                var start = DateTimeOffset.Parse(venue.Resolution.Start);
                if (start > DateTimeOffset.UtcNow)
                    return start - DateTimeOffset.UtcNow;
            }
            catch { }
        }

        // Check schedule for next occurrence today in EST
        var estNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EstZone);
        var estDayOfWeek = (int)estNow.DayOfWeek;
        var todaySchedule = venue.Schedule.FirstOrDefault(s => s.Day == estDayOfWeek);
        if (todaySchedule != null)
        {
            var startHour = todaySchedule.Start.Hour;
            var startMinute = todaySchedule.Start.Minute;
            var schedStart = new DateTime(estNow.Year, estNow.Month, estNow.Day, startHour, startMinute, 0);
            if (schedStart > estNow)
                return schedStart - estNow;
        }

        return TimeSpan.MaxValue;
    }

    private static (float startFrac, float endFrac, bool hasBar) ComputeTimelineBar(Venue venue, TimeZoneInfo tz, int lookaheadHours = 0)
    {
        // Get start/end ISO timestamps from overrides or resolution
        string? startIso = null, endIso = null;

        if (venue.ScheduleOverrides.Count > 0)
        {
            var active = venue.ScheduleOverrides.FirstOrDefault(o => o.Open && o.IsNow);
            if (active != null)
            {
                startIso = active.Start;
                endIso = active.End;
            }
        }

        if (string.IsNullOrEmpty(startIso) && venue.Resolution != null)
        {
            startIso = venue.Resolution.Start;
            endIso = venue.Resolution.End;
        }

        if (string.IsNullOrEmpty(startIso) || string.IsNullOrEmpty(endIso))
            return (0, 0, false);

        try
        {
            var startDto = DateTimeOffset.Parse(startIso);
            var endDto = DateTimeOffset.Parse(endIso);
            var now = DateTimeOffset.UtcNow;

            var isOpen = venue.Resolution?.IsNow == true ||
                         venue.ScheduleOverrides.Any(o => o.Open && o.IsNow);

            if (!isOpen && endDto <= now)
                return (0, 0, false);

            // Convert to selected timezone and position on the 2-day (48h) timeline
            var tzNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var todayMidnight = new DateTimeOffset(tzNow.Date, tz.GetUtcOffset(tzNow))
                                + TimeSpan.FromHours(lookaheadHours);
            var startEst = TimeZoneInfo.ConvertTime(startDto, tz);
            var endEst = TimeZoneInfo.ConvertTime(endDto, tz);

            var sf = (float)((startEst - todayMidnight).TotalHours / 48.0);
            var ef = (float)((endEst - todayMidnight).TotalHours / 48.0);

            return (Math.Clamp(sf, 0, 1), Math.Clamp(ef, 0, 1), true);
        }
        catch
        {
            return (0, 0, false);
        }
    }

    internal static string FormatRemainingTime(Venue venue)
    {
        var (remaining, alwaysOpen) = GetRemainingOpenInfo(venue);
        if (remaining == TimeSpan.Zero)
            return "--:--";
        if (alwaysOpen)
            return "Always";
        return $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}";
    }

    private string FormatTime(string? isoTime)
    {
        if (string.IsNullOrEmpty(isoTime))
            return "--:--";

        try
        {
            var dto = DateTimeOffset.Parse(isoTime);
            var converted = TimeZoneInfo.ConvertTime(dto, displayTimeZone);
            return converted.ToString("HH:mm");
        }
        catch
        {
            return "--:--";
        }
    }

    private (string start, string end) GetDisplayTimes(Venue venue)
    {
        if (venue.ScheduleOverrides.Count > 0)
        {
            var activeOverride = venue.ScheduleOverrides.FirstOrDefault(o => o.IsNow && o.Open)
                                 ?? venue.ScheduleOverrides.FirstOrDefault();
            if (activeOverride != null)
                return (FormatTime(activeOverride.Start), FormatTime(activeOverride.End));
        }

        if (venue.Resolution != null)
            return (FormatTime(venue.Resolution.Start), FormatTime(venue.Resolution.End));

        return ("--:--", "--:--");
    }

    private Vector4 GetVenueColor(Venue venue, string? playerRegion)
    {
        if (config.Blacklist.Contains(venue.Id))
            return new Vector4(0.6f, 0.15f, 0.15f, 0.6f);

        if (playerRegion != null && GetVenueRegion(venue) != playerRegion)
            return new Vector4(0.4f, 0.4f, 0.4f, 0.6f);

        var active = IsVenueActiveNow(venue);
        if (DataCenterColors.TryGetValue(venue.Location.DataCenter, out var color))
        {
            return active ? color : color with { W = 0.6f };
        }
        return active ? new Vector4(1, 1, 1, 1) : new Vector4(1, 1, 1, 0.6f);
    }

    internal static string GetVenueRegion(Venue venue)
    {
        return DataCenterRegions.TryGetValue(venue.Location.DataCenter, out var region) ? region : "??";
    }

    private string? GetPlayerRegion()
    {
        if (!playerState.IsLoaded)
            return lastKnownPlayerRegion;
        try
        {
            var dcName = playerState.HomeWorld.Value.DataCenter.Value.Name.ToString();
            if (DataCenterRegions.TryGetValue(dcName, out var region))
            {
                lastKnownPlayerRegion = region;
                return region;
            }
            return lastKnownPlayerRegion;
        }
        catch (InvalidOperationException)
        {
            return lastKnownPlayerRegion;
        }
    }

    internal static string BuildLifestreamCommand(VenueLocation loc)
    {
        var districtShort = loc.District.Split(' ')[0];
        var target = loc.Apartment is > 0
            ? $"A{loc.Apartment}"
            : $"P{loc.Plot}";
        return $"/li {loc.World} {districtShort} W{loc.Ward} {target}";
    }

    internal static bool IsCustomVenue(Venue venue)
    {
        return int.TryParse(venue.Id, out _);
    }

    internal HashSet<string> GetEffectiveTags(Venue venue)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (config.VenueOverrides.TryGetValue(venue.Id, out var ov))
            tags.UnionWith(ov.Tags);
        else if (IsCustomVenue(venue))
        {
            var cv = config.CustomVenues.FirstOrDefault(c => c.Id.ToString() == venue.Id);
            if (cv != null) tags.UnionWith(cv.Tags);
        }

        var descText = string.Join(" ", venue.Description);
        if (!string.IsNullOrEmpty(descText))
        {
            foreach (var tag in PredefinedTags.Concat(config.CustomTags))
            {
                if (descText.Contains(tag, StringComparison.OrdinalIgnoreCase))
                    tags.Add(tag);
            }
        }

        return tags;
    }

    internal static void ResolveCustomSchedules(Venue venue, string timezoneId, List<CustomVenueSchedule> schedules)
    {
        if (schedules.Count == 0)
            return;

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
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
        catch { }
    }

    internal Venue CustomToVenue(CustomVenue cv)
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
        ResolveCustomSchedules(venue, cv.TimezoneId, cv.Schedules);
        return venue;
    }

    internal Venue ApplyOverride(Venue apiVenue, VenueOverride ov)
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

    private bool NeedsRebuild()
    {
        if (cachedRows == null || cacheInvalidated)
            return true;
        if (!ReferenceEquals(venues, cachedVenuesRef))
            return true;
        if (config.CustomVenues.Count != cachedCustomVenueCount)
            return true;
        if (searchText != cachedSearchText)
            return true;
        if (lookaheadHours != cachedLookaheadHours)
            return true;
        if (Environment.TickCount64 - lastCacheMs >= CacheIntervalMs)
            return true;
        return false;
    }

    private void RebuildCache(string? playerRegion)
    {
        // Flow: API venues → apply overrides → add custom venues → sort
        var allVenues = new List<Venue>();
        if (venues != null)
        {
            foreach (var v in venues)
            {
                allVenues.Add(config.VenueOverrides.TryGetValue(v.Id, out var ov)
                    ? ApplyOverride(v, ov)
                    : v);
            }
        }
        foreach (var cv in config.CustomVenues)
            allVenues.Add(CustomToVenue(cv));

        // Apply search filter
        if (!string.IsNullOrEmpty(searchText))
        {
            if (searchText.StartsWith("T:", StringComparison.OrdinalIgnoreCase))
            {
                var tagQuery = searchText.Substring(2).Trim();
                if (!string.IsNullOrEmpty(tagQuery))
                    allVenues.RemoveAll(v =>
                    {
                        var tags = GetEffectiveTags(v);
                        return !tags.Any(t => t.StartsWith(tagQuery, StringComparison.OrdinalIgnoreCase));
                    });
            }
            else
            {
                allVenues.RemoveAll(v => !v.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }
        }
        cachedSearchText = searchText;

        // Pre-compute sort keys once per venue
        var sortKeys = new Dictionary<string, (TimeSpan sortTime, bool isActive, string region, TimeSpan timeUntilOpen)>(allVenues.Count);
        foreach (var v in allVenues)
        {
            var isActive = IsVenueActiveNow(v);
            sortKeys[v.Id] = (GetSortableRemainingTime(v), isActive, GetVenueRegion(v),
                isActive ? TimeSpan.Zero : GetTimeUntilOpening(v));
        }

        // Sort using pre-computed keys (no DateTimeOffset.Parse in comparator)
        var localSortCol = sortColumnIndex;
        var localSortDir = sortDirection;
        allVenues.Sort((a, b) =>
        {
            var ka = sortKeys[a.Id];
            var kb = sortKeys[b.Id];

            // Blacklisted last
            var ba = config.Blacklist.Contains(a.Id) ? 1 : 0;
            var bb = config.Blacklist.Contains(b.Id) ? 1 : 0;
            var c = ba.CompareTo(bb);
            if (c != 0) return c;

            // Favorites first
            var fa = config.Favorites.Contains(a.Id) ? 0 : 1;
            var fb = config.Favorites.Contains(b.Id) ? 0 : 1;
            c = fa.CompareTo(fb);
            if (c != 0) return c;

            if (localSortCol < 0)
            {
                // Default sort
                if (playerRegion != null)
                {
                    var ra = ka.region != playerRegion ? 1 : 0;
                    var rb = kb.region != playerRegion ? 1 : 0;
                    c = ra.CompareTo(rb);
                    if (c != 0) return c;
                }

                // Active venues first
                var oa2 = ka.isActive ? 0 : 1;
                var ob2 = kb.isActive ? 0 : 1;
                c = oa2.CompareTo(ob2);
                if (c != 0) return c;

                if (ka.isActive && kb.isActive)
                {
                    // Among active: most remaining time first
                    c = kb.sortTime.CompareTo(ka.sortTime);
                    if (c != 0) return c;
                }
                else
                {
                    // Among inactive: soonest opening first
                    c = ka.timeUntilOpen.CompareTo(kb.timeUntilOpen);
                    if (c != 0) return c;
                }

                c = a.Sfw.CompareTo(b.Sfw);
                if (c != 0) return c;
                c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return string.Compare(a.Location.World, b.Location.World, StringComparison.OrdinalIgnoreCase);
            }

            if (localSortCol is 3 or 4)
            {
                // Time-based sort
                c = ka.sortTime.CompareTo(kb.sortTime);
                return localSortDir == ImGuiSortDirection.Descending ? -c : c;
            }

            // Non-time: open first
            var oa = ka.isActive ? 0 : 1;
            var ob = kb.isActive ? 0 : 1;
            c = oa.CompareTo(ob);
            if (c != 0) return c;

            // Then by column (indices shifted: Timeline is column 5)
            c = localSortCol switch
            {
                0 => string.Compare(ka.region, kb.region, StringComparison.OrdinalIgnoreCase),
                1 => a.Sfw.CompareTo(b.Sfw),
                2 => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
                6 => string.Compare(a.Location.World, b.Location.World, StringComparison.OrdinalIgnoreCase),
                7 => string.Compare(a.Location.District, b.Location.District, StringComparison.OrdinalIgnoreCase),
                8 => a.Location.Ward.CompareTo(b.Location.Ward),
                9 => a.Location.Plot.CompareTo(b.Location.Plot),
                _ => 0,
            };
            return localSortDir == ImGuiSortDirection.Descending ? -c : c;
        });

        // Build row cache with pre-computed display data
        var rows = new List<VenueRow>(allVenues.Count);
        foreach (var venue in allVenues)
        {
            var sk = sortKeys[venue.Id];
            var times = GetDisplayTimes(venue);
            var isCustom = IsCustomVenue(venue);

            Vector4 bgColor;
            if (isCustom)
            {
                bgColor = new Vector4(10f / 255, 19f / 255, 26f / 255, 1.0f);
            }
            else if (!sk.isActive)
            {
                bgColor = new Vector4(33f / 255, 23f / 255, 26f / 255, 1.0f);
            }
            else
            {
                var (remaining, _) = GetRemainingOpenInfo(venue);
                var minutes = remaining.TotalMinutes;
                var t = Math.Clamp((minutes - 10) / (120 - 10), 0, 1);
                var r = (float)((43 * (1 - t) + 13 * t) / 255.0);
                var g = (float)((9 * (1 - t) + 36 * t) / 255.0);
                var bl = (float)((18 * (1 - t) + 9 * t) / 255.0);
                bgColor = new Vector4(r, g, bl, 1.0f);
            }

            // Always-open venues: skip lookahead shift calculation (optimization)
            var (_, isAlwaysOpen) = GetRemainingOpenInfo(venue);
            var effectiveLookahead = isAlwaysOpen ? 0 : lookaheadHours;
            var tl = ComputeTimelineBar(venue, displayTimeZone, effectiveLookahead);

            var isOtherContinent = playerRegion != null && sk.region != playerRegion;
            rows.Add(new VenueRow
            {
                Venue = venue,
                TextColor = GetVenueColor(venue, playerRegion),
                BgColor = bgColor,
                Region = sk.region,
                TimeDisplay = $"{times.start} - {times.end}",
                RemainingDisplay = FormatRemainingTime(venue),
                IsFavorite = config.Favorites.Contains(venue.Id),
                IsCustom = isCustom,
                FavId = $"*##{venue.Id}_fav",
                GoId = $"Go##{venue.Id}",
                WardStr = venue.Location.Ward.ToString(),
                PlotStr = venue.Location.Apartment is > 0
                    ? $"A{venue.Location.Apartment}"
                    : venue.Location.Plot.ToString(),
                IsApartment = venue.Location.Apartment is > 0,
                IsOtherContinent = isOtherContinent,
                Tags = GetEffectiveTags(venue),
                TimelineStartFrac = tl.startFrac,
                TimelineEndFrac = tl.endFrac,
                HasTimelineBar = tl.hasBar,
            });
        }

        cachedRows = rows;
        lastCacheMs = Environment.TickCount64;
        cacheInvalidated = false;
        cachedVenuesRef = venues;
        cachedCustomVenueCount = config.CustomVenues.Count;
        cachedLookaheadHours = lookaheadHours;
    }

    public override void Draw()
    {
        DrawTimezoneSelector();

        ImGui.SameLine();
        if (ImGui.Button("Refresh"))
            FetchVenues();

        ImGui.SameLine();
        if (ImGui.Button("Add Venue"))
            addEditWindow.OpenAdd();

        if (PopoutWindow != null)
        {
            ImGui.SameLine();
            if (ImGui.Button(PopoutWindow.IsOpen ? "Hide Popout" : "Show Popout"))
                PopoutWindow.IsOpen = !PopoutWindow.IsOpen;
        }

        ImGui.Separator();

        ImGui.SetNextItemWidth(300);
        ImGui.InputTextWithHint("##venueSearch", "Search...", ref searchText, 256);

        ImGui.SetNextItemWidth(-1);
        ImGui.SliderInt("Lookahead", ref lookaheadHours, 0, 168, "%d h");

        if (isLoading)
        {
            ImGui.TextUnformatted("Loading venues...");
            return;
        }

        if (errorMessage != null)
        {
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), $"Error: {errorMessage}");
            return;
        }

        if (venues == null || (venues.Count == 0 && config.CustomVenues.Count == 0))
        {
            ImGui.TextUnformatted("No venues found.");
            return;
        }

        DrawVenueTable();
    }

    private void DrawTimezoneSelector()
    {
        ImGui.SetNextItemWidth(300);

        var currentLabel = selectedTimezoneIndex == 0
            ? $"System Default ({TimeZoneInfo.Local.DisplayName})"
            : allTimeZones[selectedTimezoneIndex - 1].DisplayName;

        if (ImGui.BeginCombo("Timezone", currentLabel))
        {
            if (ImGui.Selectable("System Default (" + TimeZoneInfo.Local.DisplayName + ")", selectedTimezoneIndex == 0))
            {
                selectedTimezoneIndex = 0;
                displayTimeZone = TimeZoneInfo.Local;
                config.SelectedTimezoneId = "";
                config.Save();
                cacheInvalidated = true;
            }

            for (var i = 0; i < allTimeZones.Count; i++)
            {
                var isSelected = selectedTimezoneIndex == i + 1;
                if (ImGui.Selectable(allTimeZones[i].DisplayName, isSelected))
                {
                    selectedTimezoneIndex = i + 1;
                    displayTimeZone = allTimeZones[i];
                    config.SelectedTimezoneId = allTimeZones[i].Id;
                    config.Save();
                    cacheInvalidated = true;
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawVenueTable()
    {
        var playerRegion = GetPlayerRegion();

        var flags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                    | ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate;
        if (!ImGui.BeginTable("VenueTable", 11, flags))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Region", ImGuiTableColumnFlags.WidthFixed, 45);
        ImGui.TableSetupColumn("SFW", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Remaining", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("Timeline", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoSort);
        ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableSetupColumn("District", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Ward", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("Plot", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 45);
        ImGui.TableHeadersRow();

        // Read sort specs
        var sortSpecs = ImGui.TableGetSortSpecs();
        if (sortSpecs.SpecsDirty)
        {
            if (sortSpecs.SpecsCount == 0 || sortSpecs.Specs.SortDirection == ImGuiSortDirection.None)
            {
                sortColumnIndex = -1;
                sortDirection = ImGuiSortDirection.None;
            }
            else
            {
                sortColumnIndex = sortSpecs.Specs.ColumnIndex;
                sortDirection = sortSpecs.Specs.SortDirection;
            }
            sortSpecs.SpecsDirty = false;
            cacheInvalidated = true;
        }

        if (NeedsRebuild())
            RebuildCache(playerRegion);

        var rows = cachedRows!;

        // ImGuiListClipper: only draw visible rows
        var clipper = ImGui.ImGuiListClipper();
        clipper.Begin(rows.Count);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var row = rows[i];

                ImGui.TableNextRow();

                // Row background color
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(row.BgColor));

                // Region
                ImGui.TableNextColumn();
                var rowStartY = ImGui.GetCursorScreenPos().Y;
                ImGui.TextColored(row.TextColor, row.Region);

                // SFW
                ImGui.TableNextColumn();
                ImGui.TextColored(
                    row.Venue.Sfw ? new Vector4(0.3f, 1, 0.3f, 1) : new Vector4(1, 0.3f, 0.3f, 1),
                    row.Venue.Sfw ? "SFW" : "NSFW");

                // Name (star + plain text)
                ImGui.TableNextColumn();
                var starColor = row.IsFavorite
                    ? new Vector4(1f, 0.85f, 0f, 1f)
                    : new Vector4(0.5f, 0.5f, 0.5f, 0.6f);
                ImGui.PushStyleColor(ImGuiCol.Text, starColor);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 1, 1, 0.1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1, 1, 1, 0.2f));
                if (ImGui.SmallButton(row.FavId))
                {
                    if (row.IsFavorite)
                        config.Favorites.Remove(row.Venue.Id);
                    else
                        config.Favorites.Add(row.Venue.Id);
                    config.Save();
                    cacheInvalidated = true;
                }
                ImGui.PopStyleColor(4);
                ImGui.SameLine();
                ImGui.TextColored(row.TextColor, row.Venue.Name);

                // Time
                ImGui.TableNextColumn();
                ImGui.TextColored(row.TextColor, row.TimeDisplay);

                // Remaining
                ImGui.TableNextColumn();
                ImGui.TextColored(row.TextColor, row.RemainingDisplay);

                // Timeline (2 days: today left, tomorrow right)
                ImGui.TableNextColumn();
                {
                    var cursorPos = ImGui.GetCursorScreenPos();
                    var drawList = ImGui.GetWindowDrawList();
                    var colWidth = ImGui.GetContentRegionAvail().X;
                    var rowHeight = ImGui.GetTextLineHeight();

                    if (row.IsOtherContinent)
                    {
                        drawList.AddText(cursorPos, ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 0.6f)), "-");
                        ImGui.Dummy(new Vector2(colWidth, rowHeight));
                    }
                    else
                    {
                        // Background
                        drawList.AddRectFilled(
                            cursorPos,
                            new Vector2(cursorPos.X + colWidth, cursorPos.Y + rowHeight),
                            ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 0.5f)));

                        // Hour markers and day separators, shifted by lookahead
                        for (var h = lookaheadHours; h < lookaheadHours + 48; h++)
                        {
                            var frac = (h - lookaheadHours) / 48f;
                            var x = cursorPos.X + frac * colWidth;
                            if (h % 24 == 0)
                            {
                                // Day separator (white, thick)
                                drawList.AddLine(
                                    new Vector2(x, cursorPos.Y),
                                    new Vector2(x, cursorPos.Y + rowHeight),
                                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)),
                                    2f);
                            }
                            else if (h % 6 == 0)
                            {
                                drawList.AddLine(
                                    new Vector2(x, cursorPos.Y),
                                    new Vector2(x, cursorPos.Y + rowHeight),
                                    ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 0.6f)));
                            }
                            else
                            {
                                drawList.AddLine(
                                    new Vector2(x, cursorPos.Y),
                                    new Vector2(x, cursorPos.Y + rowHeight),
                                    ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 0.4f)));
                            }
                        }

                        if (row.HasTimelineBar)
                        {
                            // Schedule bar on the 48h timeline
                            var isActive = IsVenueActiveNow(row.Venue);
                            var barColor = isActive
                                ? ImGui.GetColorU32(new Vector4(0.2f, 0.8f, 0.2f, 0.8f))
                                : ImGui.GetColorU32(new Vector4(0.9f, 0.65f, 0.1f, 0.7f));
                            var barX1 = cursorPos.X + row.TimelineStartFrac * colWidth;
                            var barX2 = cursorPos.X + row.TimelineEndFrac * colWidth;
                            drawList.AddRectFilled(
                                new Vector2(barX1, cursorPos.Y + 1),
                                new Vector2(barX2, cursorPos.Y + rowHeight - 1),
                                barColor);
                        }

                        // Current time marker (yellow), shifted by lookahead
                        var estNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, displayTimeZone);
                        var nowFrac = (float)((estNow.TimeOfDay.TotalHours - lookaheadHours) / 48.0);
                        if (nowFrac is >= 0 and <= 1)
                        {
                            var nowX = cursorPos.X + nowFrac * colWidth;
                            drawList.AddLine(
                                new Vector2(nowX, cursorPos.Y),
                                new Vector2(nowX, cursorPos.Y + rowHeight),
                                ImGui.GetColorU32(new Vector4(1f, 1f, 0f, 0.9f)),
                                2f);
                        }

                        ImGui.Dummy(new Vector2(colWidth, rowHeight));
                    }
                }

                // World
                ImGui.TableNextColumn();
                ImGui.TextColored(row.TextColor, row.Venue.Location.World);

                // District
                ImGui.TableNextColumn();
                ImGui.TextColored(row.TextColor, row.Venue.Location.District);

                // Ward
                ImGui.TableNextColumn();
                ImGui.TextColored(row.TextColor, row.WardStr);

                // Plot
                ImGui.TableNextColumn();
                if (row.IsApartment)
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(new Vector4(0.1f, 0.15f, 0.35f, 1.0f)));
                ImGui.TextColored(row.TextColor, row.PlotStr);

                // Actions (Go only)
                ImGui.TableNextColumn();
                if (!row.IsOtherContinent && ImGui.SmallButton(row.GoId))
                {
                    var cmd = BuildLifestreamCommand(row.Venue.Location);
                    Plugin.Log.Information($"Jumping to venue: {cmd}");
                    Plugin.SendChatCommand(cmd);
                }

                // Right-click context menu (positional hover for full row)
                var rowH = ImGui.GetTextLineHeightWithSpacing();
                var mousePos = ImGui.GetMousePos();
                if (ImGui.IsWindowHovered() && mousePos.Y >= rowStartY && mousePos.Y < rowStartY + rowH
                    && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    contextMenuVenue = row.Venue;
                    ImGui.OpenPopup("VenueContextMenu");
                }
            }
        }
        clipper.End();
        clipper.Destroy();

        // Context menu popup (before EndTable)
        if (ImGui.BeginPopup("VenueContextMenu"))
        {
            if (contextMenuVenue != null)
            {
                var cv = contextMenuVenue;
                var isCv = IsCustomVenue(cv);

                if (!isCv && ImGui.MenuItem($"Open Venue Page ({cv.Name})"))
                {
                    Dalamud.Utility.Util.OpenLink($"https://ffxivvenues.com/venue/{cv.Id}");
                }

                if (ImGui.MenuItem($"Travel to {cv.Name}"))
                {
                    var cmd = BuildLifestreamCommand(cv.Location);
                    Plugin.Log.Information($"Jumping to venue: {cmd}");
                    Plugin.SendChatCommand(cmd);
                }

                if (ImGui.MenuItem($"Copy Lifestream command ({cv.Name})"))
                {
                    ImGui.SetClipboardText(BuildLifestreamCommand(cv.Location));
                }

                ImGui.Separator();

                if (config.Blacklist.Contains(cv.Id))
                {
                    if (ImGui.MenuItem($"Remove from Blacklist ({cv.Name})"))
                    {
                        config.Blacklist.Remove(cv.Id);
                        config.Save();
                        cacheInvalidated = true;
                    }
                }
                else
                {
                    if (ImGui.MenuItem($"Blacklist ({cv.Name})"))
                    {
                        pendingBlacklistId = cv.Id;
                        openBlacklistConfirm = true;
                        ImGui.CloseCurrentPopup();
                    }
                }

                ImGui.Separator();
                if (isCv)
                {
                    if (ImGui.MenuItem($"Edit ({cv.Name})"))
                    {
                        var customVenue = config.CustomVenues.FirstOrDefault(c => c.Id.ToString() == cv.Id);
                        if (customVenue != null)
                            addEditWindow.OpenEdit(customVenue);
                    }
                }
                else
                {
                    if (ImGui.MenuItem($"Edit ({cv.Name})"))
                    {
                        addEditWindow.OpenEditApi(cv.Id, cv);
                    }
                    if (config.VenueOverrides.ContainsKey(cv.Id))
                    {
                        if (ImGui.MenuItem($"Reset Override ({cv.Name})"))
                        {
                            config.VenueOverrides.Remove(cv.Id);
                            config.Save();
                            cacheInvalidated = true;
                        }
                    }
                }
            }
            ImGui.EndPopup();
        }

        ImGui.EndTable();

        // Blacklist confirmation modal (after EndTable)
        if (openBlacklistConfirm)
        {
            ImGui.OpenPopup("ConfirmBlacklist");
            openBlacklistConfirm = false;
        }

        var confirmOpen = true;
        if (ImGui.BeginPopupModal("ConfirmBlacklist", ref confirmOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Venue wirklich blacklisten?");
            if (ImGui.Button("Ja"))
            {
                if (pendingBlacklistId != null)
                {
                    config.Blacklist.Add(pendingBlacklistId);
                    config.Save();
                    cacheInvalidated = true;
                }
                pendingBlacklistId = null;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Nein"))
            {
                pendingBlacklistId = null;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
}
