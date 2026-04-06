using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ActiveVenueFinder.Models;

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

    private List<Venue> BuildVenueList()
    {
        var allVenues = new List<Venue>();
        if (venues != null)
        {
            foreach (var v in venues)
            {
                allVenues.Add(config.VenueOverrides.TryGetValue(v.Id, out var ov)
                    ? mainWindow.ApplyOverride(v, ov)
                    : v);
            }
        }

        foreach (var cv in config.CustomVenues)
            allVenues.Add(mainWindow.CustomToVenue(cv));

        return allVenues;
    }

    private static DateTimeOffset GetOpeningTime(Venue venue)
    {
        string? startIso = null;
        if (venue.ScheduleOverrides.Count > 0)
        {
            var active = venue.ScheduleOverrides.FirstOrDefault(o => o.Open && o.IsNow);
            if (active != null)
                startIso = active.Start;
        }

        if (string.IsNullOrEmpty(startIso) && venue.Resolution is { IsNow: true })
            startIso = venue.Resolution.Start;

        if (!string.IsNullOrEmpty(startIso))
        {
            try { return DateTimeOffset.Parse(startIso); }
            catch { }
        }

        return DateTimeOffset.MinValue;
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

        var activeVenues = allVenues
            .Where(v => VenueFinderWindow.IsVenueActiveNow(v) && !config.Blacklist.Contains(v.Id))
            .ToList();

        var favorites = activeVenues.Where(v => config.Favorites.Contains(v.Id)).ToList();
        var nonFavorites = activeVenues.Where(v => !config.Favorites.Contains(v.Id)).ToList();

        nonFavorites.Sort((a, b) => GetOpeningTime(b).CompareTo(GetOpeningTime(a)));

        var topNonFavorites = nonFavorites.Take(3).ToList();

        var displayList = new List<Venue>();
        displayList.AddRange(favorites);
        displayList.AddRange(topNonFavorites);

        if (displayList.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No active venues");
            return;
        }

        for (var i = 0; i < displayList.Count; i++)
        {
            var venue = displayList[i];
            var isFav = config.Favorites.Contains(venue.Id);
            var remaining = VenueFinderWindow.FormatRemainingTime(venue);

            var color = VenueFinderWindow.DataCenterColors.TryGetValue(venue.Location.DataCenter, out var c)
                ? c
                : new Vector4(1, 1, 1, 1);

            var prefix = isFav ? "\u2605 " : "  ";
            var label = $"{prefix}{venue.Name} ({remaining})##{venue.Id}_popout";

            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.Selectable(label);
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                var cmd = VenueFinderWindow.BuildLifestreamCommand(venue.Location);
                Plugin.Log.Information($"Popout travel: {cmd}");
                Plugin.SendChatCommand(cmd);
            }

            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                contextMenuVenue = venue;
                ImGui.OpenPopup("PopoutContextMenu");
            }
        }

        DrawContextMenu();
    }

    private void DrawContextMenu()
    {
        if (!ImGui.BeginPopup("PopoutContextMenu"))
            return;

        if (contextMenuVenue != null)
        {
            var cv = contextMenuVenue;
            var isCv = VenueFinderWindow.IsCustomVenue(cv);

            if (!isCv && ImGui.MenuItem($"Open Venue Page ({cv.Name})"))
            {
                Dalamud.Utility.Util.OpenLink($"https://ffxivvenues.com/venue/{cv.Id}");
            }

            if (ImGui.MenuItem($"Travel to {cv.Name}"))
            {
                var cmd = VenueFinderWindow.BuildLifestreamCommand(cv.Location);
                Plugin.Log.Information($"Popout travel: {cmd}");
                Plugin.SendChatCommand(cmd);
            }

            if (ImGui.MenuItem($"Copy Lifestream command ({cv.Name})"))
            {
                ImGui.SetClipboardText(VenueFinderWindow.BuildLifestreamCommand(cv.Location));
            }

            ImGui.Separator();

            if (config.Blacklist.Contains(cv.Id))
            {
                if (ImGui.MenuItem($"Remove from Blacklist ({cv.Name})"))
                {
                    config.Blacklist.Remove(cv.Id);
                    config.Save();
                }
            }
            else
            {
                if (ImGui.MenuItem($"Blacklist ({cv.Name})"))
                {
                    config.Blacklist.Add(cv.Id);
                    config.Save();
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
                    }
                }
            }
        }

        ImGui.EndPopup();
    }
}
