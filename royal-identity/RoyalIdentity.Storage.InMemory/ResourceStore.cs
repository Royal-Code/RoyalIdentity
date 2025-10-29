using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using static RoyalIdentity.Options.ServerConstants;
using System.Collections.Concurrent;
using RoyalIdentity.Models.Resources;
using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Storage.InMemory;

public class ResourceStore : IResourceStore
{
    private readonly ConcurrentDictionary<string, ResourceServer> resourceServers;
    private readonly ConcurrentDictionary<string, IdentityScope> identityResources;
    private readonly ConcurrentDictionary<string, ApiScope> apiScopes;
    private readonly ConcurrentDictionary<string, ApiResource> apiResources;

    public ResourceStore(
        ConcurrentDictionary<string, ResourceServer> resourceServers,
        ConcurrentDictionary<string, IdentityScope> identityResources,
        ConcurrentDictionary<string, ApiScope> apiScopes,
        ConcurrentDictionary<string, ApiResource> apiResources)
    {
        this.resourceServers = resourceServers;
        this.identityResources = identityResources;
        this.apiScopes = apiScopes;
        this.apiResources = apiResources;
    }

    [Obsolete("Use FindResourcesByScopeAsync instead.")]
    public Task<IEnumerable<IdentityScope>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken ct = default)
    {
        List<IdentityScope> scopes = new();
        foreach (var scope in scopeNames)
        {
            if (identityResources.TryGetValue(scope, out var identityResource))
                scopes.Add(identityResource);
        }
        return Task.FromResult<IEnumerable<IdentityScope>>(scopes);
    }

    [Obsolete("Use FindResourcesByScopeAsync instead.")]
    public Task<IEnumerable<ApiScope>> FindApiScopesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken ct = default)
    {
        List<ApiScope> scopes = new();
        foreach (var scope in scopeNames)
        {
            if (apiScopes.TryGetValue(scope, out var apiScope))
                scopes.Add(apiScope);
        }
        return Task.FromResult<IEnumerable<ApiScope>>(scopes);
    }

    [Obsolete("Use FindResourcesByScopeAsync instead.")]
    public Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken ct = default)
    {
        List<ApiResource> scopes = new();
        foreach (var scope in scopeNames)
        {
            if (this.apiResources.TryGetValue(scope, out var apiResource))
                scopes.Add(apiResource);
        }
        return Task.FromResult<IEnumerable<ApiResource>>(scopes);
    }

    public Task<AllScopes> GetAllResourcesAsync(CancellationToken ct = default)
    {
        var resources = new AllScopes(
            resourceServers.Values.ToList(),
            apiResources.Values.ToList(),
            apiScopes.Values.ToList(),
            identityResources.Values.ToList()
        );
        return Task.FromResult(resources);
    }

    public Task<AllScopes> GetAllEnabledResourcesAsync(CancellationToken ct = default)
    {
        var resources = new AllScopes(
            resourceServers.Values.Where(x => x.Enabled).ToList(),
            apiResources.Values.Where(x => x.Enabled).ToList(),
            apiScopes.Values.Where(x => x.Enabled).ToList(),
            identityResources.Values.Where(x => x.Enabled).ToList()
        );
        return Task.FromResult(resources);
    }

    public Task<RequestedScopes> FindResourcesByScopeAsync(
        IEnumerable<string> scopeNames, bool onlyEnabled = false, CancellationToken ct = default)
    {
        RequestedScopes resources = new();
        resources.Scopes.AddRange(scopeNames);

        foreach (var scope in scopeNames)
        {
            if (scope is StandardScopes.OfflineAccess)
            {
                resources.OfflineAccess = true;
                continue;
            }

            if (resourceServers.TryGetValue(scope, out var resourceServer)
                && (!onlyEnabled || resourceServer.Enabled))
            {
                if (IsEnabled(resourceServer, resources))
                    resources.ResourceServers.Add(resourceServer);
                continue;
            }

            if (identityResources.TryGetValue(scope, out var identityResource)
                && (!onlyEnabled || identityResource.Enabled))
            {
                if (IsEnabled(identityResource, resources))
                    resources.IdentityResources.Add(identityResource);
                continue;
            }

            if (apiResources.TryGetValue(scope, out var apiResource)
                && (!onlyEnabled || apiResource.Enabled))
            {
                if (IsEnabled(apiResource, resources))
                    resources.ApiResources.Add(apiResource);
                continue;
            }

            if (apiScopes.TryGetValue(scope, out var apiScope)
                && (!onlyEnabled || apiScope.Enabled))
            {
                if (IsEnabled(apiScope, resources))
                    resources.ApiScopes.Add(apiScope);
                continue;
            }

            resources.MissingScopes.Add(scope);
        }

        return Task.FromResult(resources);
    }

    private bool IsEnabled(ScopeBase scope, RequestedScopes requestedScopes)
    {
        if (scope.Enabled)
            return true;

        requestedScopes.DisabledScopes.Add(scope.Name);
        return false;
    }

    private static void AddRange<T>(ICollection<T> collection, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}