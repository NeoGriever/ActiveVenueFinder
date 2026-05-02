using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ActiveVenueFinder.Models;

namespace ActiveVenueFinder.Services.Api;

// Thrown to UI callers so they can show a friendly error without leaking transport details.
public sealed class VenueApiException : Exception
{
    public VenueApiException(string message, Exception? inner = null) : base(message, inner) { }
}

// Single owner of the HttpClient used to talk to ffxivvenues.com.
public sealed class VenueApiClient : IDisposable
{
    public const string BaseUrl = "https://api.ffxivvenues.com/v1.0/venue";

    private readonly HttpClient httpClient = new();

    // Fetches the full venue list. Cancellable; throws VenueApiException on any failure.
    public async Task<List<Venue>> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await httpClient.GetStringAsync(BaseUrl, cancellationToken).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<List<Venue>>(json);
            return result ?? new List<Venue>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new VenueApiException($"Failed to fetch venues: {ex.Message}", ex);
        }
    }

    public void Dispose() => httpClient.Dispose();
}
