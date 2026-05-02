using System;
using System.Linq;
using Dalamud.Plugin;

namespace ActiveVenueFinder.Services.Lifestream;

// Detects whether the Lifestream plugin is currently installed AND loaded. Cached with a 30 s TTL
// so we do not poll Dalamud's plugin list on every Draw frame.
public sealed class LifestreamAvailabilityService
{
    private const string LifestreamInternalName = "Lifestream";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IDalamudPluginInterface pluginInterface;
    private bool cached;
    private DateTime cachedAtUtc = DateTime.MinValue;

    public LifestreamAvailabilityService(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    // True if Lifestream is installed AND loaded. Re-evaluated at most once per cache TTL.
    public bool IsAvailable
    {
        get
        {
            var age = DateTime.UtcNow - cachedAtUtc;
            if (age > CacheTtl) Recheck();
            return cached;
        }
    }

    // Forces an immediate refresh of the cached availability flag.
    public void Recheck()
    {
        try
        {
            cached = pluginInterface.InstalledPlugins
                .Any(p => p.InternalName == LifestreamInternalName && p.IsLoaded);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Lifestream availability probe failed");
            cached = false;
        }
        cachedAtUtc = DateTime.UtcNow;
    }
}
