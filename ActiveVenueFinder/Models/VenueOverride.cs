using System;
using System.Collections.Generic;

namespace ActiveVenueFinder.Models;

[Serializable]
public sealed class VenueOverride
{
    public string Name { get; set; } = "";
    public string World { get; set; } = "";
    public string District { get; set; } = "";
    public int Ward { get; set; }
    public int Plot { get; set; }
    public int? Apartment { get; set; }
    public bool Subdivision { get; set; }
    public bool Sfw { get; set; } = true;
    public string TimezoneId { get; set; } = "America/New_York";
    public List<CustomVenueSchedule> Schedules { get; set; } = new();
    public HashSet<string> Tags { get; set; } = new();
}
