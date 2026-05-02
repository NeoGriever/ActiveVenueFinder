namespace ActiveVenueFinder.Models;

public enum VenueKeyKind
{
    Api,
    Custom,
}

// Stable identity used for cross-source per-venue features (LocalTags, future per-venue prefs).
// Serialized as "api:<id>" or "custom:<id>" so it round-trips through Newtonsoft as a plain string.
public readonly record struct VenueKey(VenueKeyKind Kind, string Value)
{
    public override string ToString() => $"{(Kind == VenueKeyKind.Api ? "api" : "custom")}:{Value}";

    public static VenueKey Api(string id) => new(VenueKeyKind.Api, id);
    public static VenueKey Custom(int id) => new(VenueKeyKind.Custom, id.ToString());

    // Treats numeric strings as custom (matches VenueResolver.IsCustomVenue convention).
    public static VenueKey FromVenueId(string venueId)
    {
        return int.TryParse(venueId, out _)
            ? new VenueKey(VenueKeyKind.Custom, venueId)
            : new VenueKey(VenueKeyKind.Api, venueId);
    }

    public static VenueKey FromVenue(Venue venue) => FromVenueId(venue.Id);

    public static bool TryParse(string raw, out VenueKey key)
    {
        key = default;
        if (string.IsNullOrEmpty(raw)) return false;
        var idx = raw.IndexOf(':');
        if (idx < 0) return false;
        var kindStr = raw.Substring(0, idx);
        var value = raw.Substring(idx + 1);
        if (kindStr == "api") { key = new VenueKey(VenueKeyKind.Api, value); return true; }
        if (kindStr == "custom") { key = new VenueKey(VenueKeyKind.Custom, value); return true; }
        return false;
    }
}
