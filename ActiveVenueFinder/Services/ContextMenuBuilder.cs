using System;
using System.Linq;
using ActiveVenueFinder.Models;
using ActiveVenueFinder.Windows;
using Dalamud.Bindings.ImGui;

namespace ActiveVenueFinder.Services;

public sealed class ContextMenuBuilder
{
    private readonly Config config;
    private readonly AddEditVenueWindow addEditWindow;
    private readonly Func<bool> isLifestreamAvailable;
    private readonly Action invalidateCache;
    private readonly Action<Venue, DoubleClickAction> dispatch;

    public ContextMenuBuilder(
        Config config,
        AddEditVenueWindow addEditWindow,
        Func<bool> isLifestreamAvailable,
        Action invalidateCache,
        Action<Venue, DoubleClickAction> dispatch)
    {
        this.config = config;
        this.addEditWindow = addEditWindow;
        this.isLifestreamAvailable = isLifestreamAvailable;
        this.invalidateCache = invalidateCache;
        this.dispatch = dispatch;
    }

    // Returns true if the user requested to open the blacklist confirmation dialog.
    public bool Draw(Venue venue, out string? blacklistRequestId)
    {
        blacklistRequestId = null;
        var isCv = VenueResolver.IsCustomVenue(venue);
        var requestedBlacklist = false;

        // Travel: only shown when Lifestream is currently available.
        if (isLifestreamAvailable())
        {
            if (ImGui.MenuItem($"Travel to {venue.Name}"))
                dispatch(venue, DoubleClickAction.LifestreamGoto);
        }

        if (ImGui.MenuItem("Open Info"))
            dispatch(venue, DoubleClickAction.OpenInfo);

        if (!isCv)
        {
            if (ImGui.MenuItem("Open Venue Page"))
                dispatch(venue, DoubleClickAction.OpenVenuePage);
        }

        // Always offered: even without Lifestream, the user might want the string for manual use.
        if (ImGui.MenuItem("Copy Lifestream command"))
            dispatch(venue, DoubleClickAction.CopyAddress);

        if (ImGui.MenuItem("Copy Name"))
            dispatch(venue, DoubleClickAction.CopyName);

        if (!isCv)
        {
            if (ImGui.MenuItem("Copy Venue Page URL"))
                dispatch(venue, DoubleClickAction.CopyVenuePageUrl);
        }

        ImGui.Separator();

        // Favorite
        var isFav = config.Favorites.Contains(venue.Id);
        if (ImGui.MenuItem(isFav ? "Remove favorite" : "Add favorite"))
        {
            if (isFav) config.Favorites.Remove(venue.Id);
            else config.Favorites.Add(venue.Id);
            config.Save();
            invalidateCache();
        }

        // Blacklist
        if (config.Blacklist.Contains(venue.Id))
        {
            if (ImGui.MenuItem("Remove from Blacklist"))
            {
                config.Blacklist.Remove(venue.Id);
                config.Save();
                invalidateCache();
            }
        }
        else
        {
            if (ImGui.MenuItem("Blacklist"))
            {
                blacklistRequestId = venue.Id;
                requestedBlacklist = true;
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.Separator();

        if (isCv)
        {
            if (ImGui.MenuItem("Edit"))
            {
                var customVenue = config.CustomVenues.FirstOrDefault(c => c.Id.ToString() == venue.Id);
                if (customVenue != null)
                    addEditWindow.OpenEdit(customVenue);
            }
            if (ImGui.MenuItem("Delete"))
            {
                config.CustomVenues.RemoveAll(c => c.Id.ToString() == venue.Id);
                config.Save();
                invalidateCache();
            }
        }
        else
        {
            if (ImGui.MenuItem("Edit (override)"))
                addEditWindow.OpenEditApi(venue.Id, venue);
            if (config.VenueOverrides.ContainsKey(venue.Id))
            {
                if (ImGui.MenuItem("Reset Override"))
                {
                    config.VenueOverrides.Remove(venue.Id);
                    config.Save();
                    invalidateCache();
                }
            }
        }

        return requestedBlacklist;
    }
}
