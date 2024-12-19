using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Storage.InMemory;

public class ResourceStore : IResourceStore
{
    private readonly MemoryStorage storage;

    public ResourceStore(MemoryStorage storage)
    {
        this.storage = storage;
    }

    public Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken ct = default)
    {
        List<IdentityResource> identityResources = new();
        foreach (var scope in scopeNames)
        {
            if (storage.IdentityResources.TryGetValue(scope, out var identityResource))
                identityResources.Add(identityResource);
        }
        return Task.FromResult<IEnumerable<IdentityResource>>(identityResources);
    }

    public Task<IEnumerable<ApiScope>> FindApiScopesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken ct = default)
    {
        List<ApiScope> apiScopes = new();
        foreach (var scope in scopeNames)
        {
            if (storage.ApiScopes.TryGetValue(scope, out var apiScope))
                apiScopes.Add(apiScope);
        }
        return Task.FromResult<IEnumerable<ApiScope>>(apiScopes);
    }

    public Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CancellationToken ct = default)
    {
        List<ApiResource> apiResources = new();
        foreach (var scope in scopeNames)
        {
            if (storage.ApiResources.TryGetValue(scope, out var apiResource))
                apiResources.Add(apiResource);
        }
        return Task.FromResult<IEnumerable<ApiResource>>(apiResources);
    }

    public Task<Resources> GetAllResourcesAsync(CancellationToken ct = default)
    {
        Resources resources = new();
        AddRange(resources.IdentityResources, storage.IdentityResources.Values);
        AddRange(resources.ApiResources, storage.ApiResources.Values);
        AddRange(resources.ApiScopes, storage.ApiScopes.Values);
        return Task.FromResult(resources);
    }

    public Task<Resources> GetAllEnabledResourcesAsync(CancellationToken ct = default)
    {
        Resources resources = new();
        AddRange(resources.IdentityResources, storage.IdentityResources.Values.Where(x => x.Enabled));
        AddRange(resources.ApiResources, storage.ApiResources.Values.Where(x => x.Enabled));
        AddRange(resources.ApiScopes, storage.ApiScopes.Values.Where(x => x.Enabled));
        return Task.FromResult(resources);
    }

    public Task<Resources> FindResourcesByScopeAsync(
        IEnumerable<string> scopeNames, bool onlyEnabled = false, CancellationToken ct = default)
    {
        Resources resources = new();

        foreach (var scope in scopeNames)
        {
            resources.RequestedScopes.Add(scope);
            if (scope is OidcConstants.StandardScopes.OfflineAccess)
                resources.OfflineAccess = true;

            if (storage.IdentityResources.TryGetValue(scope, out var identityResource)
                && (!onlyEnabled || identityResource.Enabled))
            {
                resources.IdentityResources.Add(identityResource);
            }

            if (storage.ApiResources.TryGetValue(scope, out var apiResource)
                && (!onlyEnabled || apiResource.Enabled))
            {
                resources.ApiResources.Add(apiResource);
            }

            if (storage.ApiScopes.TryGetValue(scope, out var apiScope)
                && (!onlyEnabled || apiScope.Enabled))
            {
                resources.ApiScopes.Add(apiScope);
            }
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