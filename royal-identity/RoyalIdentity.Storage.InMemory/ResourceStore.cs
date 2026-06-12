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
    private readonly Dictionary<string, (ResourceServer Server, ProtectedResource Resource)> resourceIndex;

    public ResourceStore(
        ConcurrentDictionary<string, ResourceServer> resourceServers,
        ConcurrentDictionary<string, IdentityScope> identityScopes)
    {
        this.resourceServers = resourceServers;
        this.identityScopes = identityScopes;
        scopeIndex = BuildScopeIndex(resourceServers.Values);
        resourceIndex = BuildResourceIndex(resourceServers.Values);
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
        => FindRequestedResourcesAsync(scopeNames, [], onlyEnabled, ct);

    public Task<RequestedResources> FindRequestedResourcesAsync(
        IEnumerable<string> scopeNames, IEnumerable<string> resourceUris, bool onlyEnabled = false, CancellationToken ct = default)
    {
        var resources = new RequestedResources();
        resources.RequestedScopeNames.AddRange(scopeNames);
        resources.RequestedResourceUris.AddRange(resourceUris);

        foreach (var name in resources.RequestedScopeNames)
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

        // Resource indicators (RFC 8707): match each requested URI against a protected resource.
        foreach (var uri in resources.RequestedResourceUris)
        {
            // Unknown, malformed (not an absolute URI / has a fragment), or owned by a disabled
            // resource server (Enabled derives from the parent) -> invalid_target (ADR-012).
            if (!IsValidResourceUri(uri)
                || !resourceIndex.TryGetValue(uri, out var match)
                || !match.Server.Enabled)
            {
                resources.InvalidTargets.Add(uri);
                continue;
            }

            resources.ProtectedResources.Add(match.Resource);
            AddResourceServer(resources, match.Server);
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

    /// <summary>
    /// Builds the resource-indicator index and enforces global uniqueness of <see cref="ProtectedResource.ResourceUri"/>
    /// within the realm. A resource URI belongs to exactly one resource server.
    /// </summary>
    private static Dictionary<string, (ResourceServer Server, ProtectedResource Resource)> BuildResourceIndex(
        IEnumerable<ResourceServer> servers)
    {
        var index = new Dictionary<string, (ResourceServer Server, ProtectedResource Resource)>(StringComparer.Ordinal);
        foreach (var server in servers)
        {
            foreach (var resource in server.ProtectedResources)
            {
                if (index.TryGetValue(resource.ResourceUri, out var existing))
                {
                    throw new InvalidOperationException(
                        $"Duplicate protected resource URI '{resource.ResourceUri}' found in resource servers " +
                        $"'{existing.Server.Name}' and '{server.Name}'. Resource URIs must be unique within a realm.");
                }

                index[resource.ResourceUri] = (server, resource);
            }
        }

        return index;
    }

    private static bool IsValidResourceUri(string uri)
        => Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && string.IsNullOrEmpty(parsed.Fragment);

    private static void AddResourceServer(RequestedResources resources, ResourceServer server)
    {
        if (!resources.ResourceServers.Contains(server))
            resources.ResourceServers.Add(server);
    }

    private static bool IsEnabled(ScopeBase scope, RequestedResources resources)
    {
        if (scope.Enabled)
            return true;

        // collapsed bucket (apontamento 3.1): a disabled scope is reported as invalid (MissingScopes).
        resources.MissingScopes.Add(scope.Name);
        return false;
    }
}
