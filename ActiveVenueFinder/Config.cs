using System;
using System.Collections.Generic;
using ActiveVenueFinder.Models;
using Dalamud.Configuration;

namespace ActiveVenueFinder;

[Serializable]
public sealed class Config : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public string SelectedTimezoneId = "";

    public HashSet<string> Favorites = new();
    public HashSet<string> Blacklist = new();

    public List<CustomVenue> CustomVenues = new();
    public int NextCustomVenueId = 1;

    public Dictionary<string, VenueOverride> VenueOverrides = new();
    public List<string> CustomTags = new();

    public bool PopoutOpen;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
