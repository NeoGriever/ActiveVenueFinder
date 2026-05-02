using System;
using System.Numerics;
using ActiveVenueFinder.Models;
using ActiveVenueFinder.Services;
using Dalamud.Bindings.ImGui;

namespace ActiveVenueFinder.Ui.Common;

// Renders a one-line status: loading / error+retry / "N venues, fetched HH:mm".
public static class VenueStatusBar
{
    public static void Draw(VenueRepository repository, Action onRetry)
    {
        var state = repository.State;
        switch (state.Status)
        {
            case RepositoryStatus.Loading:
                ImGui.TextColored(new Vector4(0.7f, 0.85f, 1f, 1f), "Loading venues...");
                break;
            case RepositoryStatus.Failed:
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"Error: {state.LastError}");
                ImGui.SameLine();
                if (ImGui.SmallButton("Retry##statusRetry")) onRetry();
                break;
            case RepositoryStatus.Loaded:
                var stamp = state.LastFetchUtc.HasValue
                    ? state.LastFetchUtc.Value.ToLocalTime().ToString("HH:mm")
                    : "--:--";
                ImGui.TextColored(new Vector4(0.6f, 0.7f, 0.6f, 1f),
                    $"{state.VenueCount} venues - fetched {stamp}");
                break;
            default:
                ImGui.TextUnformatted("");
                break;
        }
    }
}
