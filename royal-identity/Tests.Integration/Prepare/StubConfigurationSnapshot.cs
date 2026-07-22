using RoyalIdentity.Configuration;
using RoyalIdentity.Options;

namespace Tests.Integration.Prepare;

/// <summary>
/// A minimal <see cref="IConfigurationSnapshot"/> stub for unit-style tests that exercise a synchronous
/// configuration consumer (e.g. the cookie options configurator) directly, without standing up the hosted
/// refresher. It returns the supplied server options and realms as-is.
/// </summary>
public sealed class StubConfigurationSnapshot : IConfigurationSnapshot
{
    private readonly ServerOptions serverOptions;
    private readonly Dictionary<string, RoyalIdentity.Models.Realm> byPath;

    public StubConfigurationSnapshot(ServerOptions serverOptions, params RoyalIdentity.Models.Realm[] realms)
    {
        this.serverOptions = serverOptions;
        byPath = realms.ToDictionary(r => r.Path, StringComparer.Ordinal);
    }

    public bool IsLoaded => true;

    public ServerOptions ServerOptions => serverOptions;

    public RoyalIdentity.Models.Realm? FindRealmByPath(string path) => byPath.GetValueOrDefault(path);

    public IReadOnlyCollection<string> RealmPaths => byPath.Keys;

    public DateTimeOffset LoadedAtUtc => DateTimeOffset.UnixEpoch;

    public DateTimeOffset? LastRefreshFailureUtc => null;
}
