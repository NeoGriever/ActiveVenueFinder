using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ActiveVenueFinder.Models;
using ActiveVenueFinder.Services;
using ActiveVenueFinder.Services.Schedule;
using ActiveVenueFinder.Services.Tags;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ActiveVenueFinder.Windows;

public sealed class VenueInfoWindow : Window, IDisposable
{
    private static readonly string[] DayLabels = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
    private static readonly int[] DayApiIds = { 1, 2, 3, 4, 5, 6, 0 };

    private readonly ITextureProvider textureProvider;
    private readonly Config config;
    private readonly VenueTagService tagService;
    private readonly HttpClient httpClient = new();

    private Venue? currentVenue;
    private TimeZoneInfo displayTimeZone = TimelineCalculator.EasternTime;
    private IDalamudTextureWrap? bannerTexture;
    private CancellationTokenSource? loadCts;
    private bool bannerLoading;
    private string? bannerError;
    private string newLocalTagInput = "";

    public VenueInfoWindow(ITextureProvider textureProvider, Config config, VenueTagService tagService)
        : base("Venue Info###AvfVenueInfo", ImGuiWindowFlags.NoCollapse)
    {
        this.textureProvider = textureProvider;
        this.config = config;
        this.tagService = tagService;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 200),
            MaximumSize = new Vector2(400, float.MaxValue),
        };
        Size = new Vector2(400, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose()
    {
        ResetTexture();
        httpClient.Dispose();
    }

    public void Show(Venue venue, TimeZoneInfo displayTz)
    {
        if (IsOpen)
        {
            IsOpen = false;
        }
        currentVenue = venue;
        displayTimeZone = displayTz;
        ResetTexture();
        BeginLoadBanner(venue);
        IsOpen = true;
    }

    public override void OnClose()
    {
        ResetTexture();
        currentVenue = null;
    }

    private void ResetTexture()
    {
        loadCts?.Cancel();
        loadCts?.Dispose();
        loadCts = null;
        bannerTexture?.Dispose();
        bannerTexture = null;
        bannerLoading = false;
        bannerError = null;
    }

    private void BeginLoadBanner(Venue venue)
    {
        if (string.IsNullOrEmpty(venue.BannerUri)) return;
        var cts = new CancellationTokenSource();
        loadCts = cts;
        bannerLoading = true;
        var url = venue.BannerUri;
        Task.Run(async () =>
        {
            try
            {
                var bytes = await httpClient.GetByteArrayAsync(url, cts.Token);
                if (cts.IsCancellationRequested) return;
                var pngBytes = ConvertToPng(bytes, 400);
                if (cts.IsCancellationRequested) return;
                var tex = await textureProvider.CreateFromImageAsync(pngBytes, cancellationToken: cts.Token);
                if (cts.IsCancellationRequested)
                {
                    tex.Dispose();
                    return;
                }
                bannerTexture = tex;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                bannerError = ex.Message;
                Plugin.Log.Warning($"Venue banner load failed: {ex.Message}");
            }
            finally
            {
                bannerLoading = false;
            }
        }, cts.Token);
    }

    private static byte[] ConvertToPng(byte[] source, int targetWidth)
    {
        using var image = Image.Load<Rgba32>(source);
        if (image.Width > targetWidth)
        {
            image.Mutate(ctx => ctx.Resize(targetWidth, 0));
        }
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    public override void Draw()
    {
        if (currentVenue == null)
        {
            ImGui.TextUnformatted("No venue selected.");
            return;
        }

        var venue = currentVenue;
        var contentWidth = ImGui.GetContentRegionAvail().X;

        DrawBanner(contentWidth);

        DrawAddress(venue, contentWidth);
        ImGui.Separator();

        DrawSchedule(venue, displayTimeZone);
        ImGui.Separator();

        DrawDescription(venue);
        ImGui.Separator();

        DrawTagsSection(venue);
        ImGui.Separator();

        var hasWebsite = !string.IsNullOrEmpty(venue.Website);
        var hasDiscord = !string.IsNullOrEmpty(venue.Discord);
        if (hasWebsite || hasDiscord)
        {
            DrawLinkButtons(venue, hasWebsite, hasDiscord, contentWidth);
            ImGui.Separator();
        }

        if (!VenueResolver.IsCustomVenue(venue))
            DrawVenuePageButton(venue, contentWidth);
    }

    private void DrawBanner(float contentWidth)
    {
        if (bannerTexture != null)
        {
            var tex = bannerTexture;
            var aspect = tex.Width > 0 ? (float)tex.Height / tex.Width : 0.5f;
            var size = new Vector2(contentWidth, contentWidth * aspect);
            ImGui.Image(tex.Handle, size);
        }
        else if (bannerLoading)
        {
            ImGui.Dummy(new Vector2(contentWidth, 60));
            var pos = ImGui.GetCursorPos();
            ImGui.SetCursorPosX((contentWidth - ImGui.CalcTextSize("Loading banner...").X) * 0.5f);
            ImGui.TextDisabled("Loading banner...");
            ImGui.SetCursorPos(pos);
        }
        else if (bannerError != null)
        {
            ImGui.TextColored(new Vector4(1f, 0.5f, 0.5f, 1f), $"Banner failed: {bannerError}");
        }
    }

    private static void DrawAddress(Venue venue, float contentWidth)
    {
        var address = BuildAddress(venue);
        ImGui.Dummy(new Vector2(0, 4));
        var textSize = ImGui.CalcTextSize(address);
        ImGui.SetCursorPosX(Math.Max(0, (contentWidth - textSize.X) * 0.5f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 4));
        if (ImGui.Selectable($"{address}##avfAddr", false, ImGuiSelectableFlags.None, textSize))
            ImGui.SetClipboardText(address);
        ImGui.PopStyleVar();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to copy address");
        ImGui.Dummy(new Vector2(0, 4));
    }

    private static string BuildAddress(Venue venue)
    {
        var loc = venue.Location;
        var parts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(loc.World)) parts.Add(loc.World);
        if (!string.IsNullOrEmpty(loc.District)) parts.Add(loc.District);
        if (loc.Subdivision) parts.Add("Sub");
        parts.Add($"W{loc.Ward}");
        parts.Add(loc.Apartment is > 0 ? $"A{loc.Apartment}" : $"P{loc.Plot}");
        if (loc.Room is > 0) parts.Add($"R{loc.Room}");
        return string.Join(", ", parts);
    }

    // Renders the weekly schedule using the canonical resolver, so info-window times always match
    // the main table and timeline (including concrete API ScheduleOverrides for the current week).
    private static void DrawSchedule(Venue venue, TimeZoneInfo displayTz)
    {
        var slots = VenueScheduleResolver.GetSlotsForWeek(venue, DateTimeOffset.UtcNow);
        var perDay = VenueScheduleResolver.GroupByDisplayDay(slots, displayTz);
        for (var i = 0; i < DayLabels.Length; i++)
        {
            var label = DayLabels[i];
            var entries = perDay[i];
            if (entries.Count == 0)
            {
                ImGui.TextDisabled($"{label}  -------  -  -------");
                continue;
            }
            foreach (var (startUtc, endUtc) in entries)
            {
                var startLocal = TimeZoneInfo.ConvertTime(startUtc, displayTz);
                var endLocal = TimeZoneInfo.ConvertTime(endUtc, displayTz);
                ImGui.TextUnformatted($"{label}  {FormatTime(startLocal)} - {FormatTime(endLocal)}");
            }
        }
    }

    private static string FormatTime(DateTimeOffset t)
    {
        var hour = t.Hour;
        var h = hour % 12;
        if (h == 0) h = 12;
        var ampm = hour >= 12 ? "pm" : "am";
        return $"{h:D2}:{t.Minute:D2}{ampm}";
    }

    private static void DrawDescription(Venue venue)
    {
        if (venue.Description.Count == 0)
        {
            ImGui.TextDisabled("(no description)");
            return;
        }
        for (var i = 0; i < venue.Description.Count; i++)
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextUnformatted(venue.Description[i]);
            ImGui.PopTextWrapPos();
            if (i < venue.Description.Count - 1)
                ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeight() * 0.5f));
        }
    }

    // Renders three tag sources with distinct styles. Local tags get an X-button to remove them
    // inline; Api/Inferred chips are read-only.
    private void DrawTagsSection(Venue venue)
    {
        var entries = tagService.GetEffective(venue);
        var key = VenueKey.FromVenue(venue);

        if (entries.Count == 0)
        {
            ImGui.TextDisabled("(no tags)");
        }
        else
        {
            var avail = ImGui.GetContentRegionAvail().X;
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var startX = ImGui.GetCursorPosX();
            var lineX = 0f;
            var first = true;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var label = entry.Source == VenueTagSource.Local
                    ? entry.Tag + " x"
                    : entry.Tag;
                var w = ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2;
                if (!first)
                {
                    if (lineX + spacing + w > avail)
                    {
                        lineX = 0f;
                    }
                    else
                    {
                        ImGui.SameLine();
                        lineX += spacing;
                    }
                }
                if (lineX == 0f && !first)
                    ImGui.SetCursorPosX(startX);

                Vector4 bg, hover, active;
                switch (entry.Source)
                {
                    case VenueTagSource.Local:
                        bg = new Vector4(0.18f, 0.42f, 0.22f, 0.7f);
                        hover = new Vector4(0.25f, 0.55f, 0.30f, 0.85f);
                        active = new Vector4(0.16f, 0.40f, 0.20f, 0.7f);
                        break;
                    case VenueTagSource.Inferred:
                        bg = new Vector4(0.30f, 0.30f, 0.30f, 0.5f);
                        hover = new Vector4(0.30f, 0.30f, 0.30f, 0.5f);
                        active = new Vector4(0.30f, 0.30f, 0.30f, 0.5f);
                        break;
                    default:
                        bg = new Vector4(0.20f, 0.34f, 0.55f, 0.7f);
                        hover = new Vector4(0.25f, 0.40f, 0.62f, 0.8f);
                        active = new Vector4(0.20f, 0.34f, 0.55f, 0.7f);
                        break;
                }
                ImGui.PushStyleColor(ImGuiCol.Button, bg);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);
                if (ImGui.SmallButton(label + "##infoTag" + i) && entry.Source == VenueTagSource.Local)
                {
                    tagService.RemoveLocal(key, entry.Tag);
                }
                ImGui.PopStyleColor(3);
                lineX += w;
                first = false;
            }
        }

        ImGui.SetNextItemWidth(180);
        ImGui.InputText("##addLocalTag", ref newLocalTagInput, 64);
        ImGui.SameLine();
        if (ImGui.Button("Add Local Tag") && !string.IsNullOrWhiteSpace(newLocalTagInput))
        {
            var apiTags = venue.Tags?.ToList() ?? new System.Collections.Generic.List<string>();
            tagService.AddLocal(key, newLocalTagInput, apiTags);
            newLocalTagInput = "";
        }
    }

    private static void DrawLinkButtons(Venue venue, bool hasWebsite, bool hasDiscord, float contentWidth)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        if (hasWebsite && hasDiscord)
        {
            var w = (contentWidth - spacing) * 0.5f;
            if (ImGui.Button("Website##avfWeb", new Vector2(w, 0)))
                Dalamud.Utility.Util.OpenLink(venue.Website!);
            ImGui.SameLine();
            if (ImGui.Button("Discord##avfDc", new Vector2(w, 0)))
                Dalamud.Utility.Util.OpenLink(venue.Discord!);
        }
        else if (hasWebsite)
        {
            if (ImGui.Button("Website##avfWeb", new Vector2(contentWidth, 0)))
                Dalamud.Utility.Util.OpenLink(venue.Website!);
        }
        else if (hasDiscord)
        {
            if (ImGui.Button("Discord##avfDc", new Vector2(contentWidth, 0)))
                Dalamud.Utility.Util.OpenLink(venue.Discord!);
        }
    }

    private static void DrawVenuePageButton(Venue venue, float contentWidth)
    {
        const string label = "ffxivvenues.com page";
        var btnWidth = ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 4;
        ImGui.SetCursorPosX(Math.Max(0, (contentWidth - btnWidth) * 0.5f));
        if (ImGui.SmallButton(label))
            Dalamud.Utility.Util.OpenLink(VenueResolver.BuildVenuePageUrl(venue));
    }
}
