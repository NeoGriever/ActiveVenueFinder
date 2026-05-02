using System;

namespace ActiveVenueFinder.Models;

public enum RepositoryStatus
{
    Idle,
    Loading,
    Loaded,
    Failed,
}

public sealed class VenueRepositoryState
{
    public RepositoryStatus Status { get; set; } = RepositoryStatus.Idle;
    public DateTimeOffset? LastFetchUtc { get; set; }
    public string? LastError { get; set; }
    public int VenueCount { get; set; }
}
