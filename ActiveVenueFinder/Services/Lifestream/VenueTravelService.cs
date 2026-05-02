using ActiveVenueFinder.Models;

namespace ActiveVenueFinder.Services.Lifestream;

public sealed class VenueTravelService
{
    private readonly LifestreamAvailabilityService availability;

    public VenueTravelService(LifestreamAvailabilityService availability)
    {
        this.availability = availability;
    }

    public bool IsAvailable => availability.IsAvailable;

    // Dispatches travel only on explicit user action, and only when Lifestream is loaded.
    // Returns false if the request was refused (caller should hide travel UI in that case anyway).
    public bool TryTravel(Venue venue, out string failureReason)
    {
        if (!availability.IsAvailable)
        {
            failureReason = "Lifestream is not installed or not loaded.";
            return false;
        }
        if (string.IsNullOrEmpty(venue.Location.World) || string.IsNullOrEmpty(venue.Location.District))
        {
            failureReason = "Venue address is incomplete.";
            return false;
        }

        var cmd = LifestreamCommandBuilder.Build(venue.Location);
        Plugin.Log.Information($"Travel: {cmd}");
        Plugin.SendChatCommand(cmd);
        failureReason = "";
        return true;
    }
}
