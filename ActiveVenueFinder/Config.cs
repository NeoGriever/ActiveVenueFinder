using System;
using System.Collections.Generic;
using ActiveVenueFinder.Models;
using Dalamud.Configuration;

namespace ActiveVenueFinder;

[Serializable]
public sealed class Config : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public string SelectedTimezoneId = "";

    public HashSet<string> Favorites = new();
    public HashSet<string> Blacklist = new();

    public List<CustomVenue> CustomVenues = new();
    public int NextCustomVenueId = 1;

    public Dictionary<string, VenueOverride> VenueOverrides = new();
    public List<string> CustomTags = new();

    public bool PopoutOpen;

    // Lookahead range -72..168 (hours)
    public int InitialLookaheadHours = 0;

    // Double-click action
    public DoubleClickAction DoubleClickAction { get; set; } = DoubleClickAction.LifestreamGoto;

    // Cache interval (seconds)
    public int CacheIntervalSeconds { get; set; } = 3;

    // Appearance
    public AppearanceSettings Appearance { get; set; } = new();

    // Filter defaults persisted across sessions
    public string FilterWorld { get; set; } = "";
    public string FilterDistrict { get; set; } = "";

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
