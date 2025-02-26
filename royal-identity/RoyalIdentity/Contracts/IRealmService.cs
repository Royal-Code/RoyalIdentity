using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts;

/// <summary>
/// Service for realms.
/// </summary>
public interface IRealmService
{
    /// <summary>
    /// Get the current realm options.
    /// </summary>
    /// <returns></returns>
    public ValueTask<RealmOptions> GetOptionsAsync();

    /// <summary>
    /// Get a realm's options by its path.
    /// </summary>
    /// <param name="realmPath"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public ValueTask<RealmOptions> GetOptionsAsync(string realmPath, CancellationToken ct);
}
