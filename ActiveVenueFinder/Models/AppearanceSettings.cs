using System;
using System.Numerics;

namespace ActiveVenueFinder.Models;

[Serializable]
public sealed class AppearanceSettings
{
    // Timeline lines
    public Vector4 CurrentTimeLineColor { get; set; } = new(0f, 0.7529f, 0f, 0.95f); // #00C000
    public float CurrentTimeLineThickness { get; set; } = 2f;
    public Vector4 MidnightLineColor { get; set; } = new(0.6f, 0.6f, 0.6f, 0.9f);
    public float MidnightLineThickness { get; set; } = 2f;
    public Vector4 SixHourLineColor { get; set; } = new(0.3f, 0.3f, 0.3f, 0.6f);
    public Vector4 HourLineColor { get; set; } = new(0.18f, 0.18f, 0.18f, 0.4f);

    // Bars
    public Vector4 ActiveBarColor { get; set; } = new(0.2f, 0.8f, 0.2f, 0.85f);
    public Vector4 InactiveBarColor { get; set; } = new(0.9f, 0.65f, 0.1f, 0.7f);
    public Vector4 AlwaysOpenBarColor { get; set; } = new(0.3f, 0.6f, 1.0f, 0.85f);
    public Vector4 TimelineBgColor { get; set; } = new(0.1f, 0.1f, 0.1f, 0.5f);

    // Background variants (rows)
    public Vector4 ActiveBgGradientStart { get; set; } = new(43f / 255, 9f / 255, 18f / 255, 1f);   // running out
    public Vector4 ActiveBgGradientEnd { get; set; } = new(13f / 255, 36f / 255, 9f / 255, 1f);     // long left
    public Vector4 ClosedBgColor { get; set; } = new(33f / 255, 23f / 255, 26f / 255, 1f);
    public Vector4 CustomBgColor { get; set; } = new(10f / 255, 19f / 255, 26f / 255, 1f);
    public Vector4 ApartmentCellBgColor { get; set; } = new(0.1f, 0.15f, 0.35f, 1f);
    public Vector4 OtherContinentTextColor { get; set; } = new(0.4f, 0.4f, 0.4f, 0.6f);
    public Vector4 BlacklistTextColor { get; set; } = new(0.6f, 0.15f, 0.15f, 0.6f);

    // Favorite
    public Vector4 FavoriteIconColor { get; set; } = new(1f, 0.85f, 0f, 1f);
    public Vector4 FavoriteInactiveColor { get; set; } = new(0.5f, 0.5f, 0.5f, 0.6f);
    public Vector4 FavoriteBgColor { get; set; } = new(0.25f, 0.20f, 0.0f, 0.4f);

    // Text & sizing
    public float TextScale { get; set; } = 1.0f;
    public float BarHeightScale { get; set; } = 1.0f;
    public float LineThicknessScale { get; set; } = 1.0f;

    // SFW/NSFW colors
    public Vector4 SfwColor { get; set; } = new(0.3f, 1f, 0.3f, 1f);
    public Vector4 NsfwColor { get; set; } = new(1f, 0.3f, 0.3f, 1f);

    public AppearanceSettings Clone()
    {
        return (AppearanceSettings)MemberwiseClone();
    }

    public int ComputeHash()
    {
        var hc = new HashCode();
        hc.Add(CurrentTimeLineColor);
        hc.Add(MidnightLineColor);
        hc.Add(SixHourLineColor);
        hc.Add(HourLineColor);
        hc.Add(ActiveBarColor);
        hc.Add(InactiveBarColor);
        hc.Add(AlwaysOpenBarColor);
        hc.Add(TimelineBgColor);
        hc.Add(ActiveBgGradientStart);
        hc.Add(ActiveBgGradientEnd);
        hc.Add(ClosedBgColor);
        hc.Add(CustomBgColor);
        hc.Add(ApartmentCellBgColor);
        hc.Add(OtherContinentTextColor);
        hc.Add(BlacklistTextColor);
        hc.Add(FavoriteIconColor);
        hc.Add(FavoriteInactiveColor);
        hc.Add(FavoriteBgColor);
        hc.Add(TextScale);
        hc.Add(BarHeightScale);
        hc.Add(LineThicknessScale);
        hc.Add(SfwColor);
        hc.Add(NsfwColor);
        return hc.ToHashCode();
    }
}
