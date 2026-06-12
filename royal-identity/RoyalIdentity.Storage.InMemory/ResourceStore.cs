using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using static RoyalIdentity.Options.Constants;
using System.Collections.Concurrent;
using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Storage.InMemory;

public class ResourceStore : IResourceStore
{
    private readonly ConcurrentDictionary<string, ResourceServer> resourceServers;
    private readonly ConcurrentDictionary<string, IdentityScope> identityScopes;
    private readonly Dictionary<string, (ResourceServer Server, Scope Scope)> scopeIndex;

    public ResourceStore(
        ConcurrentDictionary<string, ResourceServer> resourceServers,
        ConcurrentDictionary<string, IdentityScope> identityScopes)
    {
        this.resourceServers = resourceServers;
        this.identityScopes = identityScopes;
        scopeIndex = BuildScopeIndex(resourceServers.Values);
    }

    public Task<AllScopes> GetAllResourcesAsync(CancellationToken ct = default)
    {
        var resources = new AllScopes(
            resourceServers.Values.ToList(),
            identityScopes.Values.ToList());
        return Task.FromResult(resources);
    }

    public Task<AllScopes> GetAllEnabledResourcesAsync(CancellationToken ct = default)
    {
        // Only enabled resource servers, and within each, only enabled scopes
        // (a disabled scope under an enabled resource server must not leak into the snapshot).
        var enabledServers = resourceServers.Values
            .Where(rs => rs.Enabled)
            .Select(rs => new ResourceServer(rs)
            {
                Scopes = [.. rs.Scopes.Where(s => s.Enabled)]
            })
            .ToList();

        var resources = new AllScopes(
            enabledServers,
            identityScopes.Values.Where(x => x.Enabled).ToList());
        return Task.FromResult(resources);
    }

    public Task<RequestedResources> FindResourcesByScopeAsync(
        IEnumerable<string> scopeNames, bool onlyEnabled = false, CancellationToken ct = default)
    {
        var resources = new RequestedResources();
        resources.RequestedScopeNames.AddRange(scopeNames);

        foreach (var name in scopeNames)
        {
            if (name == Server.StandardScopes.OfflineAccess)
            {
                resources.OfflineAccess = true;
                continue;
            }

            if (identityScopes.TryGetValue(name, out var identityScope)
                && (!onlyEnabled || identityScope.Enabled))
            {
                if (IsEnabled(identityScope, resources))
                    resources.IdentityScopes.Add(identityScope);
                continue;
            }

            // A resource server is NOT requestable by name (ADR-010): only Scope and IdentityScope are.
            // The audience is derived from the resource servers that own the requested scopes. Requesting
            // a resource server name therefore falls through to MissingScopes (invalid_scope).
            if (scopeIndex.TryGetValue(name, out var match)
                && (!onlyEnabled || (match.Server.Enabled && match.Scope.Enabled)))
            {
                if (IsEnabled(match.Server, resources) && IsEnabled(match.Scope, resources))
                {
                    resources.Scopes.Add(match.Scope);
                    AddResourceServer(resources, match.Server);
                }
                continue;
            }

            resources.MissingScopes.Add(name);
        }

        return Task.FromResult(resources);
    }

    /// <summary>
    /// Builds the scope-name index and enforces global uniqueness of scope names within the realm.
    /// A scope name belongs to exactly one resource server; duplicates are a configuration error.
    /// </summary>
    private static Dictionary<string, (ResourceServer Server, Scope Scope)> BuildScopeIndex(
        IEnumerable<ResourceServer> servers)
    {
        var index = new Dictionary<string, (ResourceServer Server, Scope Scope)>(StringComparer.Ordinal);
        foreach (var server in servers)
        {
            foreach (var scope in server.Scopes)
            {
                if (index.TryGetValue(scope.Name, out var existing))
                {
                    throw new InvalidOperationException(
                        $"Duplicate scope name '{scope.Name}' found in resource servers " +
                        $"'{existing.Server.Name}' and '{server.Name}'. Scope names must be unique within a realm.");
                }

                index[scope.Name] = (server, scope);
            }
        }

        return index;
    }

    private static void AddResourceServer(RequestedResources resources, ResourceServer server)
    {
        if (!resources.ResourceServers.Contains(server))
            resources.ResourceServers.Add(server);
    }

    private static bool IsEnabled(ScopeBase scope, RequestedResources resources)
    {
        if (scope.Enabled)
            return true;

        resources.DisabledScopes.Add(scope.Name);
        return false;
    }
}
