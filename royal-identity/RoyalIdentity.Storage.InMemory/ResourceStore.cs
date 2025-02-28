using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Extensions;
using static RoyalIdentity.Options.ServerConstants;
using System.Collections.Concurrent;

namespace RoyalIdentity.Storage.InMemory;

public class ResourceStore : IResourceStore
{
    private readonly ConcurrentDictionary<string, IdentityResource> identityResources;
    private readonly ConcurrentDictionary<string, ApiScope> apiScopes;
    private readonly ConcurrentDictionary<string, ApiResource> apiResources;

    public ResourceStore(
        ConcurrentDictionary<string, IdentityResource> identityResources,
        ConcurrentDictionary<string, ApiScope> apiScopes,
        ConcurrentDictionary<string, ApiResource> apiResources)
    {
        this.identityResources = identityResources;
        this.apiScopes = apiScopes;
        this.apiResources = apiResources;
    }

    public Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken ct = default)
    {
        List<IdentityResource> scopes = new();
        foreach (var scope in scopeNames)
        {
            if (identityResources.TryGetValue(scope, out var identityResource))
                scopes.Add(identityResource);
        }
        return Task.FromResult<IEnumerable<IdentityResource>>(scopes);
    }

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

    public Task<Resources> GetAllResourcesAsync(CancellationToken ct = default)
    {
        Resources resources = new();
        AddRange(resources.IdentityResources, identityResources.Values);
        AddRange(resources.ApiResources, apiResources.Values);
        AddRange(resources.ApiScopes, apiScopes.Values);
        return Task.FromResult(resources);
    }

    public Task<Resources> GetAllEnabledResourcesAsync(CancellationToken ct = default)
    {
        Resources resources = new();
        AddRange(resources.IdentityResources, identityResources.Values.Where(x => x.Enabled));
        AddRange(resources.ApiResources, apiResources.Values.Where(x => x.Enabled));
        AddRange(resources.ApiScopes, apiScopes.Values.Where(x => x.Enabled));
        return Task.FromResult(resources);
    }

    public Task<Resources> FindResourcesByScopeAsync(
        IEnumerable<string> scopeNames, bool onlyEnabled = false, CancellationToken ct = default)
    {
        Resources resources = new();
        resources.RequestedScopes.AddRange(scopeNames);

        foreach (var scope in scopeNames)
        {
            if (scope is StandardScopes.OfflineAccess)
            {
                resources.OfflineAccess = true;
                continue;
            }

            if (identityResources.TryGetValue(scope, out var identityResource)
                && (!onlyEnabled || identityResource.Enabled))
            {
                resources.IdentityResources.Add(identityResource);
                continue;
            }

            if (apiResources.TryGetValue(scope, out var apiResource)
                && (!onlyEnabled || apiResource.Enabled))
            {
                resources.ApiResources.Add(apiResource);
                continue;
            }

            if (apiScopes.TryGetValue(scope, out var apiScope)
                && (!onlyEnabled || apiScope.Enabled))
            {
                resources.ApiScopes.Add(apiScope);
                continue;
            }

            resources.MissingScopes.Add(scope);
        }

        return Task.FromResult(resources);
    }

    private static void AddRange<T>(ICollection<T> collection, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}