using System;
using System.Collections.Generic;
using ActiveVenueFinder.Models;
using Dalamud.Configuration;

namespace ActiveVenueFinder;

[Serializable]
public sealed class Config : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public string SelectedTimezoneId { get; set; } = "";

    public HashSet<string> Favorites { get; set; } = new();
    public HashSet<string> Blacklist { get; set; } = new();

    public List<CustomVenue> CustomVenues { get; set; } = new();
    public int NextCustomVenueId { get; set; } = 1;

    public Dictionary<string, VenueOverride> VenueOverrides { get; set; } = new();
    public List<string> CustomTags { get; set; } = new();

    // Per-venue local tags. Key is VenueKey.ToString() ("api:<id>" or "custom:<id>").
    public Dictionary<string, List<string>> LocalTags { get; set; } = new();

    // When true, scan venue descriptions for predefined+custom tag names and surface them as Inferred.
    public bool InferTagsFromDescription { get; set; } = false;

    public bool PopoutOpen { get; set; }

    // Lookahead range -72..168 (hours)
    public int InitialLookaheadHours { get; set; } = 0;

    // Default action when a venue row is double-clicked.
    public DoubleClickAction DoubleClickAction { get; set; } = DoubleClickAction.OpenInfo;

    // Cache rebuild interval (seconds) for the venue table.
    public int CacheIntervalSeconds { get; set; } = 3;

    // Show venue info popup window instead of opening the external venue page.
    public bool ShowVenueInfoPopup { get; set; } = false;

    public AppearanceSettings Appearance { get; set; } = new();

    // Persisted filter defaults.
    public string FilterWorld { get; set; } = "";
    public string FilterDistrict { get; set; } = "";

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
