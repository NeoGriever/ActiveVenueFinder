namespace ActiveVenueFinder.Models;

// Immutable view-model used by services + UI after API/override/custom merge.
public sealed class EffectiveVenue
{
    public VenueKey Key { get; }
    public Venue Source { get; }
    public bool IsCustom { get; }
    public bool HasLocalOverride { get; }

    public EffectiveVenue(VenueKey key, Venue source, bool isCustom, bool hasLocalOverride)
    {
        Key = key;
        Source = source;
        IsCustom = isCustom;
        HasLocalOverride = hasLocalOverride;
    }
}
