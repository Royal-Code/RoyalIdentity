using System.Collections.Concurrent;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Storage.EntityFramework.Configuration.Resources;

namespace Tests.Integration.Prepare;

/// <summary>
/// Explicit mutable hook for the resource/scopes bridge used only by integration-test setup. Reads return
/// snapshots and the EF resource store clones them again, so scenarios never receive shared mutable instances.
/// </summary>
public sealed class TestConfigurationResourceSource : IConfigurationResourceSource
{
    private readonly IReadOnlyList<IdentityScope> standardIdentityScopes =
        [.. new ConfigurationResourceBridgeOptions().StandardIdentityScopes];
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IdentityScope>> identityScopes =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ResourceServer>> resourceServers =
        new(StringComparer.Ordinal);

    public IEnumerable<IdentityScope> GetIdentityScopes(string realmId)
        => standardIdentityScopes.Concat(GetValues(identityScopes, realmId)).ToArray();

    public IEnumerable<ResourceServer> GetResourceServers(string realmId)
        => GetValues(resourceServers, realmId).ToArray();

    public void SetIdentityScope(string realmId, IdentityScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realmId);
        ArgumentNullException.ThrowIfNull(scope);
        GetOrAdd(identityScopes, realmId)[scope.Name] = scope;
    }

    public void SetResourceServer(string realmId, ResourceServer server)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realmId);
        ArgumentNullException.ThrowIfNull(server);
        GetOrAdd(resourceServers, realmId)[server.Name] = server;
    }

    public bool RemoveIdentityScope(string realmId, string name)
        => identityScopes.TryGetValue(realmId, out var values) && values.TryRemove(name, out _);

    public bool RemoveResourceServer(string realmId, string name)
        => resourceServers.TryGetValue(realmId, out var values) && values.TryRemove(name, out _);

    public static ResourceServer CreateDemoResourceServer()
        => new(
            ScopeVisibility.Public,
            "apiserver",
            "API Server",
            "Access to the API Server")
        {
            Scopes =
            [
                new Scope(ScopeVisibility.Public, "api", "API", "Access to the API")
                {
                    Required = true,
                },
                new Scope(ScopeVisibility.Public, "api:read", "API read", "Read values from the API")
                {
                    ShowInDiscoveryDocument = true,
                },
                new Scope(ScopeVisibility.Public, "api:write", "API write", "Write values to the API")
                {
                    Emphasize = true,
                    ShowInDiscoveryDocument = true,
                },
            ],
            ProtectedResources =
            [
                new ProtectedResource("https://api.demo.local/apiserver") { DisplayName = "API Server" },
            ],
        };

    private static IEnumerable<TValue> GetValues<TValue>(
        ConcurrentDictionary<string, ConcurrentDictionary<string, TValue>> source,
        string realmId)
        => source.TryGetValue(realmId, out var values) ? values.Values : [];

    private static ConcurrentDictionary<string, TValue> GetOrAdd<TValue>(
        ConcurrentDictionary<string, ConcurrentDictionary<string, TValue>> source,
        string realmId)
        => source.GetOrAdd(
            realmId,
            static _ => new ConcurrentDictionary<string, TValue>(StringComparer.Ordinal));
}
