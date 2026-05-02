using System;
using System.Collections.Generic;
using ActiveVenueFinder.Models;
using ActiveVenueFinder.Services.Tags;

namespace ActiveVenueFinder.Services.Filtering;

// Applies a parsed search query over a list of venues. Pure: no IO, no rebuild side-effects.
public sealed class VenueFilterService
{
    private readonly VenueTagService tagService;

    public VenueFilterService(VenueTagService tagService)
    {
        this.tagService = tagService;
    }

    // Removes venues that do not match the query in place. Returns the same list for chaining.
    public List<Venue> Apply(List<Venue> venues, VenueSearchQuery query)
    {
        if (query.TagPrefix != null)
        {
            venues.RemoveAll(v => !tagService.MatchesTagPrefix(v, query.TagPrefix));
        }
        else if (!string.IsNullOrEmpty(query.NameFragment))
        {
            venues.RemoveAll(v => !v.Name.Contains(query.NameFragment, StringComparison.OrdinalIgnoreCase));
        }

        switch (query.WorldKind)
        {
            case WorldFilterKind.DataCenter:
                venues.RemoveAll(v => !string.Equals(v.Location.DataCenter, query.WorldOrDc, StringComparison.OrdinalIgnoreCase));
                break;
            case WorldFilterKind.World:
                venues.RemoveAll(v => !string.Equals(v.Location.World, query.WorldOrDc, StringComparison.OrdinalIgnoreCase));
                break;
        }

        if (!string.IsNullOrEmpty(query.District))
        {
            venues.RemoveAll(v => !string.Equals(v.Location.District, query.District, StringComparison.OrdinalIgnoreCase));
        }

        return venues;
    }
}
