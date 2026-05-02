using ActiveVenueFinder.Models;

namespace ActiveVenueFinder.Services.Lifestream;

// Pure: turns a venue location into the chat command Lifestream listens for.
public static class LifestreamCommandBuilder
{
    public static string Build(VenueLocation loc)
    {
        var districtShort = loc.District.Split(' ')[0];
        var target = loc.Apartment is > 0
            ? $"A{loc.Apartment}"
            : $"P{loc.Plot}";
        return $"/li {loc.World} {districtShort} W{loc.Ward} {target}";
    }
}
