using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ActiveVenueFinder.Models;
using ActiveVenueFinder.Services;
using ActiveVenueFinder.Services.Filtering;
using ActiveVenueFinder.Services.Lifestream;
using ActiveVenueFinder.Services.Tags;
using ActiveVenueFinder.Ui.Common;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace ActiveVenueFinder.Windows;

public sealed class VenueFinderWindow : Window, IDisposable
{
    private const double TimelineHours = 48.0;

    private readonly IPlayerState playerState;
    private readonly Config config;
    private readonly VenueRepository repository;
    private readonly VenueTagService tagService;
    private readonly VenueTravelService travelService;
    private readonly VenueFilterService filterService;
    private readonly AddEditVenueWindow addEditWindow;
    private readonly SettingsWindow settingsWindow;
    private readonly TimezonePickerWindow timezonePicker;
    private readonly VenueInfoWindow venueInfoWindow;

    internal PopoutWindow? PopoutWindow { get; set; }

    private string? pendingBlacklistId;
    private bool openBlacklistConfirm;

    // Sort state
    private int sortColumnIndex = -1;
    private ImGuiSortDirection sortDirection = ImGuiSortDirection.None;

    // Context menu
    private Venue? contextMenuVenue;

    private TimeZoneInfo displayTimeZone = TimelineCalculator.EasternTime;

    // Display cache
    private List<VenueRow>? cachedRows;
    private long lastCacheMs;
    private bool cacheInvalidated = true;
    private List<Venue>? cachedVenuesRef;
    private List<Venue>? lastEffectiveSnapshot;
    private int cachedCustomVenueCount = -1;
    private string? lastKnownPlayerRegion;
    private string searchText = "";
    private string cachedSearchText = "";
    private string filterWorld = "";
    private string filterDistrict = "";
    private string cachedFilterWorld = "";
    private string cachedFilterDistrict = "";
    private int lookaheadHours;
    private int cachedLookaheadHours;
    private DateTimeOffset cachedWindowStartUtc;
    private int cachedAppearanceHash;
    private string cachedTimeZoneId = "";

    // Discovered districts (for District-Filter dropdown)
    private List<string> availableDistricts = new();

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
        public string WardStr = "";
        public string PlotStr = "";
        public bool IsApartment;
        public bool IsOtherContinent;
        public bool IsActive;
        public List<TimeSlot> Slots = new();
        public HashSet<string> Tags = new();
    }

    public VenueFinderWindow(
        Config config,
        IPlayerState playerState,
        VenueRepository repository,
        VenueTagService tagService,
        VenueTravelService travelService,
        AddEditVenueWindow addEditWindow,
        SettingsWindow settingsWindow,
        TimezonePickerWindow timezonePicker,
        VenueInfoWindow venueInfoWindow)
        : base("Active Venue Finder###ActiveVenueFinder")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(700, 250),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(1400, 600);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.config = config;
        this.playerState = playerState;
        this.repository = repository;
        this.tagService = tagService;
        this.travelService = travelService;
        this.filterService = new VenueFilterService(tagService);
        this.addEditWindow = addEditWindow;
        this.settingsWindow = settingsWindow;
        this.timezonePicker = timezonePicker;
        this.venueInfoWindow = venueInfoWindow;
        this.addEditWindow.OnSaved = () =>
        {
            cacheInvalidated = true;
            repository.NotifyConfigChanged();
        };
        this.settingsWindow.OnAppearanceChanged = () => cacheInvalidated = true;
        repository.Changed += OnRepositoryChanged;

        if (!string.IsNullOrEmpty(config.SelectedTimezoneId))
        {
            try { displayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(config.SelectedTimezoneId); }
            catch { displayTimeZone = TimelineCalculator.EasternTime; }
        }

        lookaheadHours = config.InitialLookaheadHours;
        filterWorld = config.FilterWorld;
        filterDistrict = config.FilterDistrict;

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Comments,
            Priority = 90,
            Click = _ =>
            {
                Dalamud.Utility.Util.OpenLink("https://discord.gg/HBh4nSbuJp");
            },
            ShowTooltip = () =>
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip("Join my discord for\n- bug reports\n- ideas\n- faq\n- just talk\n\n(still working on it!)");
            }
        });

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Coffee,
            Priority = 100,
            Click = _ =>
            {
                Dalamud.Utility.Util.OpenLink("https://buymeacoffee.com/mindconstructor");
            },
            ShowTooltip = () =>
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip("If you like the plugin,\nthink about to spend me something\nthrough buy me a coffee.\n\n<3 <3 <3");
            }
        });

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cogs,
            Priority = 110,
            Click = _ =>
            {
                settingsWindow.IsOpen = !settingsWindow.IsOpen;
            },
            ShowTooltip = () =>
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip("Open the configuration window");
            }
        });
    }

    public void Dispose()
    {
        repository.Changed -= OnRepositoryChanged;
    }

    public override void OnOpen() => repository.RefreshAsync(force: false);

    public List<Venue> GetResolvedVenues() => repository.GetEffectiveVenues();

    // Repository state changed: invalidate the row cache so the next Draw rebuilds from fresh data.
    private void OnRepositoryChanged()
    {
        cacheInvalidated = true;
    }

    public void Refresh() => repository.RefreshAsync(force: true);

    private string? GetPlayerRegion()
    {
        if (!playerState.IsLoaded) return lastKnownPlayerRegion;
        try
        {
            var dcName = playerState.HomeWorld.Value.DataCenter.Value.Name.ToString();
            if (VenueResolver.DataCenterRegions.TryGetValue(dcName, out var region))
            {
                lastKnownPlayerRegion = region;
                return region;
            }
            return lastKnownPlayerRegion;
        }
        catch (InvalidOperationException) { return lastKnownPlayerRegion; }
    }

    // Effective tag set for filtering: API + Local + (optional) Inferred via VenueTagService.
    private HashSet<string> GetEffectiveTags(Venue venue)
    {
        var entries = tagService.GetEffective(venue);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries) set.Add(e.Tag);
        return set;
    }

    // Routes a row interaction to the user-configured action. LifestreamGoto silently falls back
    // to OpenInfo when Lifestream is not currently available, so the click never dead-ends.
    public void DispatchAction(Venue venue, DoubleClickAction action)
    {
        if (action == DoubleClickAction.LifestreamGoto && !travelService.IsAvailable)
            action = DoubleClickAction.OpenInfo;

        switch (action)
        {
            case DoubleClickAction.LifestreamGoto:
                travelService.TryTravel(venue, out _);
                break;
            case DoubleClickAction.OpenVenuePage:
            {
                if (VenueResolver.IsCustomVenue(venue)) break;
                if (config.ShowVenueInfoPopup)
                    venueInfoWindow.Show(venue, displayTimeZone);
                else
                    Dalamud.Utility.Util.OpenLink(VenueResolver.BuildVenuePageUrl(venue));
                break;
            }
            case DoubleClickAction.OpenInfo:
                venueInfoWindow.Show(venue, displayTimeZone);
                break;
            case DoubleClickAction.CopyAddress:
                ImGui.SetClipboardText(LifestreamCommandBuilder.Build(venue.Location));
                break;
            case DoubleClickAction.CopyName:
                ImGui.SetClipboardText(venue.Name);
                break;
            case DoubleClickAction.CopyVenuePageUrl:
                if (!VenueResolver.IsCustomVenue(venue))
                    ImGui.SetClipboardText(VenueResolver.BuildVenuePageUrl(venue));
                break;
            case DoubleClickAction.None:
            default:
                break;
        }
    }

    private Vector4 GetVenueColor(Venue venue, string? playerRegion, bool isActive)
    {
        var a = config.Appearance;
        if (config.Blacklist.Contains(venue.Id))
            return a.BlacklistTextColor;
        if (playerRegion != null && VenueResolver.GetVenueRegion(venue) != playerRegion)
            return a.OtherContinentTextColor;
        if (VenueResolver.DataCenterColors.TryGetValue(venue.Location.DataCenter, out var color))
            return isActive ? color : color with { W = 0.6f };
        return isActive ? new Vector4(1, 1, 1, 1) : new Vector4(1, 1, 1, 0.6f);
    }

    private DateTimeOffset ComputeWindowStartUtc()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var tzNow = TimeZoneInfo.ConvertTime(nowUtc, displayTimeZone);
        var localMidnight = tzNow.Date;
        var midnightOffset = displayTimeZone.GetUtcOffset(localMidnight);
        var midnightUtc = new DateTimeOffset(localMidnight, midnightOffset).ToUniversalTime();
        return midnightUtc + TimeSpan.FromHours(lookaheadHours);
    }

    private bool NeedsRebuild()
    {
        if (cachedRows == null || cacheInvalidated) return true;
        if (config.CustomVenues.Count != cachedCustomVenueCount) return true;
        if (searchText != cachedSearchText) return true;
        if (filterWorld != cachedFilterWorld) return true;
        if (filterDistrict != cachedFilterDistrict) return true;
        if (lookaheadHours != cachedLookaheadHours) return true;
        if (displayTimeZone.Id != cachedTimeZoneId) return true;
        if (config.Appearance.ComputeHash() != cachedAppearanceHash) return true;
        if (Environment.TickCount64 - lastCacheMs >= config.CacheIntervalSeconds * 1000L) return true;
        return false;
    }

    private void RebuildCache(string? playerRegion)
    {
        var allVenues = repository.GetEffectiveVenues();
        lastEffectiveSnapshot = allVenues;

        // Apply parsed search query (name OR T:tag prefix) + world/dc/district filters via service.
        var query = VenueSearchQuery.Parse(searchText, filterWorld, filterDistrict);
        filterService.Apply(allVenues, query);
        cachedSearchText = searchText;
        cachedFilterWorld = filterWorld;
        cachedFilterDistrict = filterDistrict;

        // Compute window
        var windowStartUtc = ComputeWindowStartUtc();
        var windowEndUtc = windowStartUtc + TimeSpan.FromHours(TimelineHours);

        // Slots per venue
        var slotsPerVenue = new Dictionary<string, List<TimeSlot>>(allVenues.Count);
        foreach (var v in allVenues)
        {
            var horizon = TimeSpan.FromHours(Math.Max(72, TimelineHours - lookaheadHours));
            // For sort/timing we need a wider lookahead than just the visible window:
            // include past 24h and future 7 days regardless of window.
            var nowUtc = DateTimeOffset.UtcNow;
            var sortWindowStart = (windowStartUtc < nowUtc - TimeSpan.FromDays(1)) ? windowStartUtc : nowUtc - TimeSpan.FromDays(1);
            var sortWindowEnd = (windowEndUtc > nowUtc + TimeSpan.FromDays(7)) ? windowEndUtc : nowUtc + TimeSpan.FromDays(7);
            slotsPerVenue[v.Id] = TimelineCalculator.GetSlotsInWindow(v, sortWindowStart, sortWindowEnd);
        }

        // Sort keys
        var sortKeys = new Dictionary<string, (TimeSpan sortTime, bool isActive, string region, TimeSpan timeUntilOpen)>(allVenues.Count);
        var nowUtc2 = DateTimeOffset.UtcNow;
        foreach (var v in allVenues)
        {
            var slots = slotsPerVenue[v.Id];
            var active = TimelineCalculator.GetActiveSlot(slots);
            var isActive = active.HasValue;
            TimeSpan sortTime;
            if (active.HasValue)
            {
                sortTime = active.Value.AlwaysOpen ? TimeSpan.FromMinutes(1) : (active.Value.EndUtc - nowUtc2);
            }
            else
            {
                var next = TimelineCalculator.GetNextSlot(slots, nowUtc2);
                sortTime = next.HasValue ? -(next.Value.StartUtc - nowUtc2) : TimeSpan.MinValue;
            }
            var nextOpen = TimelineCalculator.GetNextSlot(slots, nowUtc2);
            var timeUntilOpen = nextOpen.HasValue ? nextOpen.Value.StartUtc - nowUtc2 : TimeSpan.MaxValue;
            sortKeys[v.Id] = (sortTime, isActive, VenueResolver.GetVenueRegion(v), timeUntilOpen);
        }

        // Sort
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
                if (playerRegion != null)
                {
                    var ra = ka.region != playerRegion ? 1 : 0;
                    var rb = kb.region != playerRegion ? 1 : 0;
                    c = ra.CompareTo(rb);
                    if (c != 0) return c;
                }
                var oa = ka.isActive ? 0 : 1;
                var ob = kb.isActive ? 0 : 1;
                c = oa.CompareTo(ob);
                if (c != 0) return c;

                if (ka.isActive && kb.isActive)
                {
                    c = kb.sortTime.CompareTo(ka.sortTime);
                    if (c != 0) return c;
                }
                else
                {
                    c = ka.timeUntilOpen.CompareTo(kb.timeUntilOpen);
                    if (c != 0) return c;
                }

                c = a.Sfw.CompareTo(b.Sfw);
                if (c != 0) return c;
                c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return string.Compare(a.Location.World, b.Location.World, StringComparison.OrdinalIgnoreCase);
            }

            // Time-column sort (col 9)
            if (localSortCol == 9)
            {
                c = ka.sortTime.CompareTo(kb.sortTime);
                return localSortDir == ImGuiSortDirection.Descending ? -c : c;
            }

            // Other: open first, then column
            var oaC = ka.isActive ? 0 : 1;
            var obC = kb.isActive ? 0 : 1;
            c = oaC.CompareTo(obC);
            if (c != 0) return c;

            c = localSortCol switch
            {
                0 => string.Compare(ka.region, kb.region, StringComparison.OrdinalIgnoreCase),
                1 => string.Compare(a.Location.World, b.Location.World, StringComparison.OrdinalIgnoreCase),
                2 => string.Compare(a.Location.District, b.Location.District, StringComparison.OrdinalIgnoreCase),
                3 => a.Location.Ward.CompareTo(b.Location.Ward),
                4 => a.Location.Plot.CompareTo(b.Location.Plot),
                5 => a.Sfw.CompareTo(b.Sfw),
                6 => (config.Favorites.Contains(a.Id) ? 0 : 1).CompareTo(config.Favorites.Contains(b.Id) ? 0 : 1),
                7 => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
                _ => 0,
            };
            return localSortDir == ImGuiSortDirection.Descending ? -c : c;
        });

        // Build VenueRows
        var rows = new List<VenueRow>(allVenues.Count);
        var districtSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var appearance = config.Appearance;

        foreach (var venue in allVenues)
        {
            var sk = sortKeys[venue.Id];
            var slots = slotsPerVenue[venue.Id];
            var isCustom = VenueResolver.IsCustomVenue(venue);

            Vector4 bgColor;
            if (isCustom)
            {
                bgColor = appearance.CustomBgColor;
            }
            else if (!sk.isActive)
            {
                bgColor = appearance.ClosedBgColor;
            }
            else
            {
                var (rem, _) = TimelineCalculator.GetRemainingFromActive(slots);
                var minutes = rem.TotalMinutes;
                var t = (float)Math.Clamp((minutes - 10) / (120 - 10), 0, 1);
                bgColor = Vector4.Lerp(appearance.ActiveBgGradientStart, appearance.ActiveBgGradientEnd, t);
            }

            // Time display: based on the *active* slot if any, else the next upcoming.
            string timeDisplay;
            string remainingDisplay;
            var active = TimelineCalculator.GetActiveSlot(slots);
            if (active.HasValue)
            {
                var startLocal = TimeZoneInfo.ConvertTime(active.Value.StartUtc, displayTimeZone);
                var endLocal = TimeZoneInfo.ConvertTime(active.Value.EndUtc, displayTimeZone);
                timeDisplay = $"{startLocal:HH:mm} - {endLocal:HH:mm}";
                if (active.Value.AlwaysOpen)
                {
                    remainingDisplay = "Always";
                }
                else
                {
                    var rem = active.Value.EndUtc - DateTimeOffset.UtcNow;
                    if (rem <= TimeSpan.Zero) remainingDisplay = "--:--";
                    else remainingDisplay = $"{(int)rem.TotalHours:D2}:{rem.Minutes:D2}";
                }
            }
            else
            {
                var next = TimelineCalculator.GetNextSlot(slots, DateTimeOffset.UtcNow);
                if (next.HasValue)
                {
                    var startLocal = TimeZoneInfo.ConvertTime(next.Value.StartUtc, displayTimeZone);
                    var endLocal = TimeZoneInfo.ConvertTime(next.Value.EndUtc, displayTimeZone);
                    timeDisplay = $"{startLocal:HH:mm} - {endLocal:HH:mm}";
                    remainingDisplay = "--:--";
                }
                else
                {
                    timeDisplay = "--:-- - --:--";
                    remainingDisplay = "--:--";
                }
            }

            var isOtherContinent = playerRegion != null && sk.region != playerRegion;

            rows.Add(new VenueRow
            {
                Venue = venue,
                TextColor = GetVenueColor(venue, playerRegion, sk.isActive),
                BgColor = bgColor,
                Region = sk.region,
                TimeDisplay = timeDisplay,
                RemainingDisplay = remainingDisplay,
                IsFavorite = config.Favorites.Contains(venue.Id),
                IsCustom = isCustom,
                WardStr = venue.Location.Ward.ToString(),
                PlotStr = venue.Location.Apartment is > 0
                    ? $"A{venue.Location.Apartment}"
                    : $"P{venue.Location.Plot}",
                IsApartment = venue.Location.Apartment is > 0,
                IsOtherContinent = isOtherContinent,
                IsActive = sk.isActive,
                Slots = slots,
                Tags = GetEffectiveTags(venue),
            });

            if (!string.IsNullOrEmpty(venue.Location.District))
                districtSet.Add(venue.Location.District);
        }

        availableDistricts = districtSet.OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();

        cachedRows = rows;
        lastCacheMs = Environment.TickCount64;
        cacheInvalidated = false;
        cachedVenuesRef = lastEffectiveSnapshot;
        cachedCustomVenueCount = config.CustomVenues.Count;
        cachedLookaheadHours = lookaheadHours;
        cachedWindowStartUtc = windowStartUtc;
        cachedAppearanceHash = appearance.ComputeHash();
        cachedTimeZoneId = displayTimeZone.Id;
    }

    public override void Draw()
    {
        DrawHeader();
        ImGui.Separator();
        VenueStatusBar.Draw(repository, () => repository.RefreshAsync(force: true));
        ImGui.Separator();

        var status = repository.State.Status;
        if (status == RepositoryStatus.Loading && !repository.HasData)
        {
            return;
        }
        if (status == RepositoryStatus.Failed && !repository.HasData && config.CustomVenues.Count == 0)
        {
            return;
        }
        if (!repository.HasData && config.CustomVenues.Count == 0)
        {
            ImGui.TextUnformatted("No venues found.");
            return;
        }

        // Reserve ~36px for the lookahead slider at the bottom
        var avail = ImGui.GetContentRegionAvail();
        var listHeight = Math.Max(50, avail.Y - 38);

        if (ImGui.BeginChild("##VenueListContainer", new Vector2(0, listHeight)))
        {
            DrawVenueTable();
        }
        ImGui.EndChild();

        DrawLookaheadSlider();
    }

    private void DrawHeader()
    {
        // Search input (left)
        ImGui.SetNextItemWidth(220);
        if (ImGui.InputTextWithHint("##venueSearch", "Search... (T:tag)", ref searchText, 256))
            cacheInvalidated = true;

        ImGui.SameLine();
        DrawWorldFilter();

        ImGui.SameLine();
        DrawDistrictFilter();

        ImGui.SameLine();
        ImGui.TextUnformatted("|");
        ImGui.SameLine();

        if (ImGui.Button("Refresh"))
            repository.RefreshAsync(force: true);

        ImGui.SameLine();
        if (ImGui.Button("+"))
            addEditWindow.OpenAdd();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add Venue");

        if (PopoutWindow != null)
        {
            ImGui.SameLine();
            if (ImGui.Button(PopoutWindow.IsOpen ? "<-" : "->"))
                PopoutWindow.IsOpen = !PopoutWindow.IsOpen;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(PopoutWindow.IsOpen ? "Hide Popout" : "Show Popout");
        }

        ImGui.SameLine();
        if (ImGui.Button("Settings"))
            settingsWindow.IsOpen = !settingsWindow.IsOpen;

        ImGui.SameLine();
        var tzLabel = TimezoneRegistry.FormatLocal(displayTimeZone);
        if (ImGui.Button($"TZ: {tzLabel}"))
        {
            timezonePicker.OpenPicker(entry =>
            {
                displayTimeZone = entry.TimeZone;
                cacheInvalidated = true;
            });
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Choose display timezone");

        ImGui.SameLine();
        ImGui.TextUnformatted("|");
        ImGui.SameLine();

        // UTC + Local
        var utc = DateTime.UtcNow;
        var local = DateTime.Now;
        ImGui.TextColored(new Vector4(0.7f, 0.85f, 1f, 1f),
            $"UTC {utc:HH:mm} - Local {local:HH:mm}");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"UTC: {utc:yyyy-MM-dd HH:mm:ss}\nLocal: {local:yyyy-MM-dd HH:mm:ss zzz}");
    }

    private void DrawWorldFilter()
    {
        ImGui.SetNextItemWidth(180);
        var label = string.IsNullOrEmpty(filterWorld) ? "World: any"
            : filterWorld.StartsWith("DC:") ? $"DC: {filterWorld.Substring(3)}"
            : filterWorld.StartsWith("World:") ? $"W: {filterWorld.Substring(6)}"
            : filterWorld;

        if (ImGui.BeginCombo("##worldFilter", label))
        {
            if (ImGui.Selectable("Any", string.IsNullOrEmpty(filterWorld)))
            {
                filterWorld = "";
                config.FilterWorld = "";
                config.Save();
                cacheInvalidated = true;
            }

            // Group by DC
            var dcGroups = VenueResolver.WorldToDataCenter
                .GroupBy(kv => kv.Value)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var dcGroup in dcGroups)
            {
                ImGui.Separator();
                var dc = dcGroup.Key;
                var dcSelected = filterWorld == $"DC:{dc}";
                var dcColor = VenueResolver.DataCenterColors.TryGetValue(dc, out var cc) ? cc : new Vector4(1, 1, 1, 1);
                ImGui.PushStyleColor(ImGuiCol.Text, dcColor);
                if (ImGui.Selectable($"[DC] {dc}", dcSelected))
                {
                    filterWorld = $"DC:{dc}";
                    config.FilterWorld = filterWorld;
                    config.Save();
                    cacheInvalidated = true;
                }
                ImGui.PopStyleColor();

                foreach (var kv in dcGroup.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var w = kv.Key;
                    var wSelected = filterWorld == $"World:{w}";
                    ImGui.PushStyleColor(ImGuiCol.Text, dcColor with { W = 0.85f });
                    if (ImGui.Selectable($"    {w}", wSelected))
                    {
                        filterWorld = $"World:{w}";
                        config.FilterWorld = filterWorld;
                        config.Save();
                        cacheInvalidated = true;
                    }
                    ImGui.PopStyleColor();
                }
            }
            ImGui.EndCombo();
        }
    }

    private void DrawDistrictFilter()
    {
        ImGui.SetNextItemWidth(160);
        var label = string.IsNullOrEmpty(filterDistrict) ? "District: any" : filterDistrict;
        if (ImGui.BeginCombo("##districtFilter", label))
        {
            if (ImGui.Selectable("Any", string.IsNullOrEmpty(filterDistrict)))
            {
                filterDistrict = "";
                config.FilterDistrict = "";
                config.Save();
                cacheInvalidated = true;
            }
            foreach (var d in availableDistricts)
            {
                if (ImGui.Selectable(d, filterDistrict == d))
                {
                    filterDistrict = d;
                    config.FilterDistrict = d;
                    config.Save();
                    cacheInvalidated = true;
                }
            }
            ImGui.EndCombo();
        }
    }

    private void DrawLookaheadSlider()
    {
        ImGui.SetNextItemWidth(-100);
        if (ImGui.SliderInt("##lookahead", ref lookaheadHours, -72, 168, $"%+d h  (-3d ... +7d)"))
            cacheInvalidated = true;
        ImGui.SameLine();
        if (ImGui.Button("Now##lookaheadNow"))
        {
            lookaheadHours = 0;
            cacheInvalidated = true;
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
        ImGui.TableSetupColumn("R", ImGuiTableColumnFlags.WidthFixed, 35);                        // 0
        ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 90);                    // 1
        ImGui.TableSetupColumn("District", ImGuiTableColumnFlags.WidthFixed, 120);                // 2
        ImGui.TableSetupColumn("Ward", ImGuiTableColumnFlags.WidthFixed, 40);                     // 3
        ImGui.TableSetupColumn("Plot/Apt", ImGuiTableColumnFlags.WidthFixed, 50);                 // 4
        ImGui.TableSetupColumn("SFW", ImGuiTableColumnFlags.WidthFixed, 50);                      // 5
        ImGui.TableSetupColumn("*", ImGuiTableColumnFlags.WidthFixed, 28);                        // 6
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 220);                    // 7
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 36); // 8
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 110);                    // 9
        ImGui.TableSetupColumn("Timebar", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoSort); // 10
        ImGui.TableHeadersRow();

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
        var appearance = config.Appearance;

        var clipper = ImGui.ImGuiListClipper();
        clipper.Begin(rows.Count);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                DrawRow(rows[i], appearance);
            }
        }
        clipper.End();
        clipper.Destroy();

        DrawContextMenu();

        ImGui.EndTable();

        DrawBlacklistConfirm();
    }

    private void DrawRow(VenueRow row, AppearanceSettings appearance)
    {
        ImGui.TableNextRow();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(row.BgColor));
        var rowStartY = ImGui.GetCursorScreenPos().Y;

        // 0: R
        ImGui.TableNextColumn();
        ImGui.TextColored(row.TextColor, row.Region);

        // 1: World
        ImGui.TableNextColumn();
        ImGui.TextColored(row.TextColor, row.Venue.Location.World);

        // 2: District
        ImGui.TableNextColumn();
        ImGui.TextColored(row.TextColor, row.Venue.Location.District);

        // 3: Ward
        ImGui.TableNextColumn();
        ImGui.TextColored(row.TextColor, row.WardStr);

        // 4: Plot/Apt
        ImGui.TableNextColumn();
        if (row.IsApartment)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(appearance.ApartmentCellBgColor));
        ImGui.TextColored(row.TextColor, row.PlotStr);

        // 5: SFW
        ImGui.TableNextColumn();
        ImGui.TextColored(
            row.Venue.Sfw ? appearance.SfwColor : appearance.NsfwColor,
            row.Venue.Sfw ? "SFW" : "NSFW");

        // 6: Favorite
        ImGui.TableNextColumn();
        if (row.IsFavorite)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(appearance.FavoriteBgColor));
        var favColor = row.IsFavorite ? appearance.FavoriteIconColor : appearance.FavoriteInactiveColor;
        ImGui.PushStyleColor(ImGuiCol.Text, favColor);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 1, 1, 0.1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1, 1, 1, 0.2f));
        if (ImGui.SmallButton($"*##fav{row.Venue.Id}"))
        {
            if (row.IsFavorite) config.Favorites.Remove(row.Venue.Id);
            else config.Favorites.Add(row.Venue.Id);
            config.Save();
            cacheInvalidated = true;
        }
        ImGui.PopStyleColor(4);

        // 7: Name (with double-click)
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.Text, row.TextColor);
        ImGui.Selectable($"{row.Venue.Name}##nm{row.Venue.Id}", false, ImGuiSelectableFlags.AllowDoubleClick);
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            DispatchAction(row.Venue, config.DoubleClickAction);

        // 8: Action (Go arrow). Hidden when Lifestream is unavailable or the venue is offshore.
        ImGui.TableNextColumn();
        if (!row.IsOtherContinent && travelService.IsAvailable)
        {
            if (ImGui.SmallButton($"->##go{row.Venue.Id}"))
            {
                DispatchAction(row.Venue, DoubleClickAction.LifestreamGoto);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Travel via Lifestream");
        }

        // 9: Time
        ImGui.TableNextColumn();
        ImGui.TextColored(row.TextColor, $"{row.TimeDisplay}  ({row.RemainingDisplay})");

        // 10: Timebar
        ImGui.TableNextColumn();
        DrawTimelineCell(row, appearance);

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

    private void DrawTimelineCell(VenueRow row, AppearanceSettings appearance)
    {
        var cursorPos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var colWidth = ImGui.GetContentRegionAvail().X;
        var rowHeight = ImGui.GetTextLineHeight() * appearance.BarHeightScale;

        if (row.IsOtherContinent)
        {
            drawList.AddText(cursorPos, ImGui.GetColorU32(appearance.OtherContinentTextColor), "-");
            ImGui.Dummy(new Vector2(colWidth, rowHeight));
            return;
        }

        // Background
        drawList.AddRectFilled(
            cursorPos,
            new Vector2(cursorPos.X + colWidth, cursorPos.Y + rowHeight),
            ImGui.GetColorU32(appearance.TimelineBgColor));

        // Hour markers / day separators (computed in displayTimeZone).
        // For each integer hour in [0..48], compute the absolute UTC time and check the local hour.
        var windowStartUtc = cachedWindowStartUtc;
        for (var h = 0; h <= (int)TimelineHours; h++)
        {
            var frac = h / (float)TimelineHours;
            var x = cursorPos.X + frac * colWidth;
            var atUtc = windowStartUtc + TimeSpan.FromHours(h);
            var atLocal = TimeZoneInfo.ConvertTime(atUtc, displayTimeZone);
            var localHour = atLocal.Hour;

            uint color;
            float thickness;
            if (localHour == 0)
            {
                color = ImGui.GetColorU32(appearance.MidnightLineColor);
                thickness = appearance.MidnightLineThickness * appearance.LineThicknessScale;
            }
            else if (localHour % 6 == 0)
            {
                color = ImGui.GetColorU32(appearance.SixHourLineColor);
                thickness = 1f * appearance.LineThicknessScale;
            }
            else
            {
                color = ImGui.GetColorU32(appearance.HourLineColor);
                thickness = 1f * appearance.LineThicknessScale;
            }
            drawList.AddLine(
                new Vector2(x, cursorPos.Y),
                new Vector2(x, cursorPos.Y + rowHeight),
                color, thickness);
        }

        // Slot bars
        var windowEndUtc = windowStartUtc + TimeSpan.FromHours(TimelineHours);
        foreach (var slot in row.Slots)
        {
            if (slot.EndUtc <= windowStartUtc || slot.StartUtc >= windowEndUtc)
                continue;
            var startFrac = (float)Math.Clamp((slot.StartUtc - windowStartUtc).TotalHours / TimelineHours, 0, 1);
            var endFrac = (float)Math.Clamp((slot.EndUtc - windowStartUtc).TotalHours / TimelineHours, 0, 1);
            if (endFrac - startFrac < 0.001f) continue;

            Vector4 col;
            if (slot.AlwaysOpen) col = appearance.AlwaysOpenBarColor;
            else if (slot.IsActive) col = appearance.ActiveBarColor;
            else col = appearance.InactiveBarColor;

            var x1 = cursorPos.X + startFrac * colWidth;
            var x2 = cursorPos.X + endFrac * colWidth;
            drawList.AddRectFilled(
                new Vector2(x1, cursorPos.Y + 1),
                new Vector2(x2, cursorPos.Y + rowHeight - 1),
                ImGui.GetColorU32(col));
        }

        // Current time marker (green)
        var nowUtc = DateTimeOffset.UtcNow;
        var nowFrac = (float)((nowUtc - windowStartUtc).TotalHours / TimelineHours);
        if (nowFrac >= 0 && nowFrac <= 1)
        {
            var nowX = cursorPos.X + nowFrac * colWidth;
            drawList.AddLine(
                new Vector2(nowX, cursorPos.Y),
                new Vector2(nowX, cursorPos.Y + rowHeight),
                ImGui.GetColorU32(appearance.CurrentTimeLineColor),
                appearance.CurrentTimeLineThickness * appearance.LineThicknessScale);
        }

        ImGui.Dummy(new Vector2(colWidth, rowHeight));
    }

    private void DrawContextMenu()
    {
        if (!ImGui.BeginPopup("VenueContextMenu")) return;
        if (contextMenuVenue != null)
        {
            var builder = new ContextMenuBuilder(config, addEditWindow,
                () => travelService.IsAvailable,
                () => cacheInvalidated = true,
                DispatchAction);
            if (builder.Draw(contextMenuVenue, out var blacklistId) && blacklistId != null)
            {
                pendingBlacklistId = blacklistId;
                openBlacklistConfirm = true;
            }
        }
        ImGui.EndPopup();
    }

    private void DrawBlacklistConfirm()
    {
        if (openBlacklistConfirm)
        {
            ImGui.OpenPopup("ConfirmBlacklist");
            openBlacklistConfirm = false;
        }
        var confirmOpen = true;
        if (ImGui.BeginPopupModal("ConfirmBlacklist", ref confirmOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Really blacklist this venue?");
            if (ImGui.Button("Yes"))
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
            if (ImGui.Button("No"))
            {
                pendingBlacklistId = null;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
}
