using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ActiveVenueFinder.Models;

[Serializable]
public sealed class CustomVenue
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string World { get; set; } = "";
    public string District { get; set; } = "";
    public int Ward { get; set; }
    public int Plot { get; set; }
    public int? Apartment { get; set; }
    public bool Subdivision { get; set; }
    public bool Sfw { get; set; } = true;
    public List<CustomVenueSchedule> Schedules { get; set; } = new();

    // Legacy v1 fields preserved only so ConfigMigrator can move data out. Never read by new code.
    // Cleared by the migrator. Hidden from public consumers via EditorBrowsable.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public HashSet<string> Tags { get; set; } = new();

    [EditorBrowsable(EditorBrowsableState.Never)]
    public string TimezoneId { get; set; } = "";
}

[Serializable]
public sealed class CustomVenueSchedule
{
    public DayOfWeek Day { get; set; }
    public int StartHour { get; set; }
    public int StartMinute { get; set; }
    public int EndHour { get; set; }
    public int EndMinute { get; set; }
    public bool NextDay { get; set; }
}
