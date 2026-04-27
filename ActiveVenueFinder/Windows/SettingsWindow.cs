using System;
using System.Numerics;
using ActiveVenueFinder.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ActiveVenueFinder.Windows;

public sealed class SettingsWindow : Window
{
    private readonly Config config;
    private long lastDirtyMs;
    private bool dirty;
    private string newCustomTagInput = "";

    public Action? OnAppearanceChanged { get; set; }

    public SettingsWindow(Config config)
        : base("Active Venue Finder - Settings###AvfSettings")
    {
        this.config = config;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(450, 400),
            MaximumSize = new Vector2(900, 1200),
        };
        Size = new Vector2(580, 700);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    private void MarkDirty()
    {
        dirty = true;
        lastDirtyMs = Environment.TickCount64;
        OnAppearanceChanged?.Invoke();
    }

    public override void Draw()
    {
        // Debounced save (500ms after last change)
        if (dirty && Environment.TickCount64 - lastDirtyMs > 500)
        {
            config.Save();
            dirty = false;
        }

        DrawGeneral();
        DrawColorsLines();
        DrawColorsBars();
        DrawColorsBackgrounds();
        DrawFavorite();
        DrawTextSizing();
        DrawTags();

        if (dirty)
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f), "Saving...");
        }
    }

    public override void OnClose()
    {
        if (dirty)
        {
            config.Save();
            dirty = false;
        }
    }

    // ─── Sections ─────────────────────────────────────

    private void DrawGeneral()
    {
        if (!ImGui.CollapsingHeader("General", ImGuiTreeNodeFlags.DefaultOpen)) return;

        var dca = (int)config.DoubleClickAction;
        var actionLabels = new[] { "None", "Lifestream goto", "Open Venue Page", "Copy address (/li)", "Copy name", "Copy Venue Page URL" };
        if (ImGui.Combo("Double-click action", ref dca, actionLabels, actionLabels.Length))
        {
            config.DoubleClickAction = (DoubleClickAction)dca;
            MarkDirty();
        }

        var cache = config.CacheIntervalSeconds;
        if (ImGui.SliderInt("Cache refresh (s)", ref cache, 1, 10))
        {
            config.CacheIntervalSeconds = cache;
            MarkDirty();
        }

        var initLook = config.InitialLookaheadHours;
        if (ImGui.SliderInt("Initial lookahead (h)", ref initLook, -72, 168, "%+d h"))
        {
            config.InitialLookaheadHours = initLook;
            MarkDirty();
        }
    }

    private void DrawColorsLines()
    {
        if (!ImGui.CollapsingHeader("Colors — Timeline lines")) return;
        var a = config.Appearance;

        var c = a.CurrentTimeLineColor;
        if (ImGui.ColorEdit4("Current time line", ref c)) { a.CurrentTimeLineColor = c; MarkDirty(); }
        var ct = a.CurrentTimeLineThickness;
        if (ImGui.SliderFloat("Current line thickness", ref ct, 1f, 4f)) { a.CurrentTimeLineThickness = ct; MarkDirty(); }

        c = a.MidnightLineColor;
        if (ImGui.ColorEdit4("Midnight line", ref c)) { a.MidnightLineColor = c; MarkDirty(); }
        var mt = a.MidnightLineThickness;
        if (ImGui.SliderFloat("Midnight thickness", ref mt, 1f, 4f)) { a.MidnightLineThickness = mt; MarkDirty(); }

        c = a.SixHourLineColor;
        if (ImGui.ColorEdit4("6-hour marker", ref c)) { a.SixHourLineColor = c; MarkDirty(); }

        c = a.HourLineColor;
        if (ImGui.ColorEdit4("Hourly marker", ref c)) { a.HourLineColor = c; MarkDirty(); }

        c = a.TimelineBgColor;
        if (ImGui.ColorEdit4("Timeline background", ref c)) { a.TimelineBgColor = c; MarkDirty(); }

        if (ImGui.Button("Reset lines to defaults##linesReset"))
        {
            var def = new AppearanceSettings();
            a.CurrentTimeLineColor = def.CurrentTimeLineColor;
            a.CurrentTimeLineThickness = def.CurrentTimeLineThickness;
            a.MidnightLineColor = def.MidnightLineColor;
            a.MidnightLineThickness = def.MidnightLineThickness;
            a.SixHourLineColor = def.SixHourLineColor;
            a.HourLineColor = def.HourLineColor;
            a.TimelineBgColor = def.TimelineBgColor;
            MarkDirty();
        }
    }

    private void DrawColorsBars()
    {
        if (!ImGui.CollapsingHeader("Colors — Timeline bars")) return;
        var a = config.Appearance;

        var c = a.ActiveBarColor;
        if (ImGui.ColorEdit4("Active bar", ref c)) { a.ActiveBarColor = c; MarkDirty(); }

        c = a.InactiveBarColor;
        if (ImGui.ColorEdit4("Inactive (upcoming) bar", ref c)) { a.InactiveBarColor = c; MarkDirty(); }

        c = a.AlwaysOpenBarColor;
        if (ImGui.ColorEdit4("Always-open bar", ref c)) { a.AlwaysOpenBarColor = c; MarkDirty(); }

        if (ImGui.Button("Reset bars to defaults##barsReset"))
        {
            var def = new AppearanceSettings();
            a.ActiveBarColor = def.ActiveBarColor;
            a.InactiveBarColor = def.InactiveBarColor;
            a.AlwaysOpenBarColor = def.AlwaysOpenBarColor;
            MarkDirty();
        }
    }

    private void DrawColorsBackgrounds()
    {
        if (!ImGui.CollapsingHeader("Colors — Row backgrounds")) return;
        var a = config.Appearance;

        var c = a.ActiveBgGradientStart;
        if (ImGui.ColorEdit4("Active bg (running out)", ref c)) { a.ActiveBgGradientStart = c; MarkDirty(); }

        c = a.ActiveBgGradientEnd;
        if (ImGui.ColorEdit4("Active bg (long left)", ref c)) { a.ActiveBgGradientEnd = c; MarkDirty(); }

        c = a.ClosedBgColor;
        if (ImGui.ColorEdit4("Closed bg", ref c)) { a.ClosedBgColor = c; MarkDirty(); }

        c = a.CustomBgColor;
        if (ImGui.ColorEdit4("Custom-venue bg", ref c)) { a.CustomBgColor = c; MarkDirty(); }

        c = a.ApartmentCellBgColor;
        if (ImGui.ColorEdit4("Apartment cell bg", ref c)) { a.ApartmentCellBgColor = c; MarkDirty(); }

        c = a.OtherContinentTextColor;
        if (ImGui.ColorEdit4("Other-continent text", ref c)) { a.OtherContinentTextColor = c; MarkDirty(); }

        c = a.BlacklistTextColor;
        if (ImGui.ColorEdit4("Blacklisted text", ref c)) { a.BlacklistTextColor = c; MarkDirty(); }

        c = a.SfwColor;
        if (ImGui.ColorEdit4("SFW text", ref c)) { a.SfwColor = c; MarkDirty(); }

        c = a.NsfwColor;
        if (ImGui.ColorEdit4("NSFW text", ref c)) { a.NsfwColor = c; MarkDirty(); }

        if (ImGui.Button("Reset backgrounds to defaults##bgReset"))
        {
            var def = new AppearanceSettings();
            a.ActiveBgGradientStart = def.ActiveBgGradientStart;
            a.ActiveBgGradientEnd = def.ActiveBgGradientEnd;
            a.ClosedBgColor = def.ClosedBgColor;
            a.CustomBgColor = def.CustomBgColor;
            a.ApartmentCellBgColor = def.ApartmentCellBgColor;
            a.OtherContinentTextColor = def.OtherContinentTextColor;
            a.BlacklistTextColor = def.BlacklistTextColor;
            a.SfwColor = def.SfwColor;
            a.NsfwColor = def.NsfwColor;
            MarkDirty();
        }
    }

    private void DrawFavorite()
    {
        if (!ImGui.CollapsingHeader("Favorites")) return;
        var a = config.Appearance;

        var c = a.FavoriteIconColor;
        if (ImGui.ColorEdit4("Favorite icon (active)", ref c)) { a.FavoriteIconColor = c; MarkDirty(); }

        c = a.FavoriteInactiveColor;
        if (ImGui.ColorEdit4("Favorite icon (inactive)", ref c)) { a.FavoriteInactiveColor = c; MarkDirty(); }

        c = a.FavoriteBgColor;
        if (ImGui.ColorEdit4("Favorite cell bg", ref c)) { a.FavoriteBgColor = c; MarkDirty(); }
    }

    private void DrawTextSizing()
    {
        if (!ImGui.CollapsingHeader("Text & sizes")) return;
        var a = config.Appearance;

        var ts = a.TextScale;
        if (ImGui.SliderFloat("Text scale", ref ts, 0.8f, 1.5f)) { a.TextScale = ts; MarkDirty(); }

        var bh = a.BarHeightScale;
        if (ImGui.SliderFloat("Bar height scale", ref bh, 0.5f, 2f)) { a.BarHeightScale = bh; MarkDirty(); }

        var lt = a.LineThicknessScale;
        if (ImGui.SliderFloat("Line thickness scale", ref lt, 0.5f, 3f)) { a.LineThicknessScale = lt; MarkDirty(); }
    }

    private void DrawTags()
    {
        if (!ImGui.CollapsingHeader("Custom tags")) return;

        ImGui.SetNextItemWidth(180);
        ImGui.InputTextWithHint("##newCt", "New tag name", ref newCustomTagInput, 32);
        ImGui.SameLine();
        if (ImGui.Button("Add##addCt") && !string.IsNullOrWhiteSpace(newCustomTagInput))
        {
            var t = newCustomTagInput.Trim();
            if (!config.CustomTags.Contains(t))
            {
                config.CustomTags.Add(t);
                MarkDirty();
            }
            newCustomTagInput = "";
        }

        for (var i = config.CustomTags.Count - 1; i >= 0; i--)
        {
            var t = config.CustomTags[i];
            ImGui.TextUnformatted(t);
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##ct{i}"))
            {
                config.CustomTags.RemoveAt(i);
                MarkDirty();
            }
        }
    }

}
