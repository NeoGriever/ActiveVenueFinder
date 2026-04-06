using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ActiveVenueFinder.Models;

public sealed class Venue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sfw")]
    public bool Sfw { get; set; }

    [JsonPropertyName("location")]
    public VenueLocation Location { get; set; } = new();

    [JsonPropertyName("schedule")]
    public List<VenueSchedule> Schedule { get; set; } = new();

    [JsonPropertyName("scheduleOverrides")]
    public List<VenueScheduleOverride> ScheduleOverrides { get; set; } = new();

    [JsonPropertyName("resolution")]
    public VenueResolution? Resolution { get; set; }

    [JsonPropertyName("description")]
    public List<string> Description { get; set; } = new();
}

public sealed class VenueLocation
{
    [JsonPropertyName("dataCenter")]
    public string DataCenter { get; set; } = string.Empty;

    [JsonPropertyName("world")]
    public string World { get; set; } = string.Empty;

    [JsonPropertyName("district")]
    public string District { get; set; } = string.Empty;

    [JsonPropertyName("ward")]
    public int Ward { get; set; }

    [JsonPropertyName("plot")]
    public int Plot { get; set; }

    [JsonPropertyName("apartment")]
    public int? Apartment { get; set; }

    [JsonPropertyName("room")]
    public int? Room { get; set; }

    [JsonPropertyName("subdivision")]
    public bool Subdivision { get; set; }
}

public sealed class VenueSchedule
{
    [JsonPropertyName("day")]
    public int Day { get; set; }

    [JsonPropertyName("start")]
    public VenueTime Start { get; set; } = new();

    [JsonPropertyName("end")]
    public VenueTime End { get; set; } = new();

    [JsonPropertyName("resolution")]
    public VenueResolution? Resolution { get; set; }
}

public sealed class VenueTime
{
    [JsonPropertyName("hour")]
    public int Hour { get; set; }

    [JsonPropertyName("minute")]
    public int Minute { get; set; }

    [JsonPropertyName("timeZone")]
    public string TimeZone { get; set; } = string.Empty;

    [JsonPropertyName("nextDay")]
    public bool NextDay { get; set; }
}

public sealed class VenueScheduleOverride
{
    [JsonPropertyName("open")]
    public bool Open { get; set; }

    [JsonPropertyName("start")]
    public string Start { get; set; } = string.Empty;

    [JsonPropertyName("end")]
    public string End { get; set; } = string.Empty;

    [JsonPropertyName("isNow")]
    public bool IsNow { get; set; }
}

public sealed class VenueResolution
{
    [JsonPropertyName("start")]
    public string Start { get; set; } = string.Empty;

    [JsonPropertyName("end")]
    public string End { get; set; } = string.Empty;

    [JsonPropertyName("isNow")]
    public bool IsNow { get; set; }

    [JsonPropertyName("isWithinWeek")]
    public bool IsWithinWeek { get; set; }
}
