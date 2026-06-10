namespace RoyalIdentity.Options;

/// <summary>
/// Options for caching.
/// </summary>
public class CacheOptions
{
    public CacheOptions()
    {
    }

    public CacheOptions(CacheOptions other)
    {
        KeyCacheDuration = other.KeyCacheDuration;
    }

    /// <summary>
    /// Gets or sets the duration for caching keys.
    /// </summary>
    public TimeSpan KeyCacheDuration { get; set; } = TimeSpan.FromMinutes(5);
}
