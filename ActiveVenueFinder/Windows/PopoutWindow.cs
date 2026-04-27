using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using ActiveVenueFinder.Models;
using ActiveVenueFinder.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ActiveVenueFinder.Windows;

public sealed class PopoutWindow : Window, IDisposable
{
    private readonly Config config;
    private readonly VenueFinderWindow mainWindow;
    private readonly AddEditVenueWindow addEditWindow;
    private readonly HttpClient httpClient = new();

    private List<Venue>? venues;
    private bool isLoading;
    private long lastFetchTicks;
    private const long FetchIntervalMs = 15 * 60 * 1000;

    private Venue? contextMenuVenue;
    private string? pendingBlacklistId;
    private bool openBlacklistConfirm;

    public PopoutWindow(Config config, VenueFinderWindow mainWindow, AddEditVenueWindow addEditWindow)
        : base("##PopoutVenueFinder",
               ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)
    {
        this.config = config;
        this.mainWindow = mainWindow;
        this.addEditWindow = addEditWindow;

        Size = new Vector2(350, 0);
        SizeCondition = ImGuiCond.FirstUseEver;

        IsOpen = config.PopoutOpen;
        if (IsOpen)
            FetchVenues();
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    public override void OnOpen()
    {
        config.PopoutOpen = true;
        config.Save();
        if (venues == null)
            FetchVenues();
    }

    public override void OnClose()
    {
        config.PopoutOpen = false;
        config.Save();
    }

    private void FetchVenues()
    {
        isLoading = true;
        lastFetchTicks = Environment.TickCount64;
        Task.Run(async () =>
        {
            try
            {
                var json = await httpClient.GetStringAsync(VenueFinderWindow.ApiUrl);
                var result = JsonSerializer.Deserialize<List<Venue>>(json);
                venues = result ?? new List<Venue>();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Popout fetch failed: {ex}");
            }
            finally
            {
                isLoading = false;
            }
        });
    }

    private List<Venue> BuildVenueList() => VenueResolver.BuildAll(venues, config);

    private static string FormatRemaining(List<TimeSlot> slots)
    {
        var (rem, alwaysOpen) = TimelineCalculator.GetRemainingFromActive(slots);
        if (alwaysOpen) return "Always";
        if (rem <= TimeSpan.Zero) return "--:--";
        return $"{(int)rem.TotalHours:D2}:{rem.Minutes:D2}";
    }

    public override void Draw()
    {
        if (Environment.TickCount64 - lastFetchTicks >= FetchIntervalMs)
            FetchVenues();

        if (isLoading && venues == null)
        {
            ImGui.TextUnformatted("Loading...");
            return;
        }

        if (venues == null)
            return;

        var allVenues = BuildVenueList();
        var nowUtc = DateTimeOffset.UtcNow;
        var windowStart = nowUtc - TimeSpan.FromHours(1);
        var windowEnd = nowUtc + TimeSpan.FromDays(1);

        var entries = new List<(Venue venue, List<TimeSlot> slots, DateTimeOffset openedAt)>();
        foreach (var v in allVenues)
        {
            if (config.Blacklist.Contains(v.Id)) continue;
            var slots = TimelineCalculator.GetSlotsInWindow(v, windowStart, windowEnd);
            var active = TimelineCalculator.GetActiveSlot(slots);
            if (!active.HasValue) continue;
            entries.Add((v, slots, active.Value.StartUtc));
        }

        var favorites = entries.Where(e => config.Favorites.Contains(e.venue.Id)).ToList();
        var nonFavorites = entries.Where(e => !config.Favorites.Contains(e.venue.Id))
            .OrderByDescending(e => e.openedAt)
            .Take(3)
            .ToList();

        var displayList = favorites.Concat(nonFavorites).ToList();

        if (displayList.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No active venues");
            return;
        }

        foreach (var entry in displayList)
        {
            var venue = entry.venue;
            var isFav = config.Favorites.Contains(venue.Id);
            var remaining = FormatRemaining(entry.slots);

            var color = VenueResolver.DataCenterColors.TryGetValue(venue.Location.DataCenter, out var c)
                ? c
                : new Vector4(1, 1, 1, 1);

            var prefix = isFav ? "* " : "  ";
            var label = $"{prefix}{venue.Name} ({remaining})##{venue.Id}_popout";

            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.Selectable(label, false, ImGuiSelectableFlags.AllowDoubleClick);
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                mainWindow.DispatchAction(venue, config.DoubleClickAction);

            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                contextMenuVenue = venue;
                ImGui.OpenPopup("PopoutContextMenu");
            }
        }

        DrawContextMenu();
        DrawBlacklistConfirm();
    }

    private void DrawContextMenu()
    {
        if (!ImGui.BeginPopup("PopoutContextMenu")) return;
        if (contextMenuVenue != null)
        {
            var builder = new ContextMenuBuilder(config, addEditWindow,
                () => { /* popout has no cache */ },
                mainWindow.DispatchAction);
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
            ImGui.OpenPopup("PopoutConfirmBlacklist");
            openBlacklistConfirm = false;
        }
        var confirmOpen = true;
        if (ImGui.BeginPopupModal("PopoutConfirmBlacklist", ref confirmOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Venue wirklich blacklisten?");
            if (ImGui.Button("Ja"))
            {
                if (pendingBlacklistId != null)
                {
                    config.Blacklist.Add(pendingBlacklistId);
                    config.Save();
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
