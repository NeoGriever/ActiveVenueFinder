using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ActiveVenueFinder.Models;
using ActiveVenueFinder.Services.Api;
using Dalamud.Plugin;

namespace ActiveVenueFinder.Services;

// Orchestrates venue data: API fetch, override application, custom-venue inclusion.
// Owns the only HttpClient (via VenueApiClient). Cancels overlapping refreshes.
public sealed class VenueRepository : IDisposable
{
    private readonly VenueApiClient apiClient;
    private readonly Config config;
    private readonly IDalamudPluginInterface pluginInterface;

    private List<Venue>? rawApiVenues;
    private CancellationTokenSource? currentFetchCts;
    private readonly object fetchLock = new();
    private const long MinRefreshIntervalMs = 5_000;

    // Fired whenever rawApiVenues changes or status transitions. Marshalled to UI thread.
    public event Action? Changed;

    public VenueRepositoryState State { get; } = new();

    public VenueRepository(VenueApiClient apiClient, Config config, IDalamudPluginInterface pluginInterface)
    {
        this.apiClient = apiClient;
        this.config = config;
        this.pluginInterface = pluginInterface;
    }

    // Returns the merged venue list: API venues with overrides applied + custom venues.
    public List<Venue> GetEffectiveVenues() => VenueResolver.BuildAll(rawApiVenues, config);

    // True if a successful fetch has been completed at least once.
    public bool HasData => rawApiVenues != null;

    // Triggers a fetch. Cancels any in-flight fetch. If force=false, short-circuits when
    // the last successful fetch is recent.
    public void RefreshAsync(bool force = false)
    {
        if (!force && State.LastFetchUtc.HasValue
            && (DateTimeOffset.UtcNow - State.LastFetchUtc.Value).TotalMilliseconds < MinRefreshIntervalMs
            && State.Status == RepositoryStatus.Loaded)
        {
            return;
        }

        CancellationTokenSource cts;
        lock (fetchLock)
        {
            currentFetchCts?.Cancel();
            currentFetchCts?.Dispose();
            cts = new CancellationTokenSource();
            currentFetchCts = cts;
        }

        State.Status = RepositoryStatus.Loading;
        FireChanged();

        _ = Task.Run(async () =>
        {
            try
            {
                var venues = await apiClient.FetchAsync(cts.Token).ConfigureAwait(false);
                if (cts.IsCancellationRequested) return;

                rawApiVenues = venues;
                State.Status = RepositoryStatus.Loaded;
                State.LastFetchUtc = DateTimeOffset.UtcNow;
                State.LastError = null;
                State.VenueCount = venues.Count;
                FireChanged();
            }
            catch (OperationCanceledException)
            {
                // expected on overlapping refresh
            }
            catch (VenueApiException ex)
            {
                State.Status = RepositoryStatus.Failed;
                State.LastError = ex.Message;
                Plugin.Log.Error(ex, "Venue fetch failed");
                FireChanged();
            }
            catch (Exception ex)
            {
                State.Status = RepositoryStatus.Failed;
                State.LastError = ex.Message;
                Plugin.Log.Error(ex, "Unexpected error during venue fetch");
                FireChanged();
            }
        });
    }

    // Notify listeners on the UI thread via a one-shot Draw subscription.
    private void FireChanged()
    {
        void OneShot()
        {
            pluginInterface.UiBuilder.Draw -= OneShot;
            try { Changed?.Invoke(); }
            catch (Exception ex) { Plugin.Log.Error(ex, "VenueRepository.Changed handler threw"); }
        }
        pluginInterface.UiBuilder.Draw += OneShot;
    }

    // Allows external callers (e.g. config edits) to invalidate cached effective list.
    public void NotifyConfigChanged() => FireChanged();

    public void Dispose()
    {
        lock (fetchLock)
        {
            currentFetchCts?.Cancel();
            currentFetchCts?.Dispose();
            currentFetchCts = null;
        }
    }
}
