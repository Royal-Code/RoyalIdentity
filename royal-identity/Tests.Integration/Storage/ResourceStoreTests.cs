using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Scopes;
using Tests.Integration.Prepare;

namespace Tests.Integration.Storage;

/// <summary>
/// Resource catalog invariants exercised through the canonical EF configuration gateway.
/// </summary>
public class ResourceStoreTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public ResourceStoreTests(PersistentStorageAppFactory factory) => this.factory = factory;

    private static ResourceServer Api(string name, params Scope[] scopes)
        => new(ScopeVisibility.Public, name, name, name) { Scopes = [.. scopes] };

    private static Scope Op(string name, bool enabled = true)
        => new(ScopeVisibility.Public, name, name, name) { Enabled = enabled };

    private static ProtectedResource Resource(string uri) => new(uri);

    private async Task<TResult> WithStoreAsync<TResult>(
        ResourceServer[] servers,
        Func<IResourceStore, Task<TResult>> operation)
    {
        factory.Resources.ReplaceResourceServers(factory.Handles.Demo.Id, servers);
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        return await factory.WithStorageAsync(
            storage => operation(storage.GetResourceStore(realm)));
    }

    [Fact]
    public async Task FindResourcesByScope_DisabledScope_IsReportedAsInvalid()
    {
        var server = Api("api1", Op("api1.read"), Op("api1.write", enabled: false));
        var resources = await WithStoreAsync(
            [server],
            store => store.FindResourcesByScopeAsync(["api1.read", "api1.write"]));

        Assert.Contains(resources.Scopes, scope => scope.Name == "api1.read");
        Assert.DoesNotContain(resources.Scopes, scope => scope.Name == "api1.write");
        Assert.Contains("api1.write", resources.MissingScopes);
        Assert.False(resources.IsValid);
    }

    [Fact]
    public async Task FindResourcesByScope_OnlyEnabled_DisabledScope_IsReportedAsInvalid()
    {
        var server = Api("api1", Op("api1.read"), Op("api1.write", enabled: false));
        var resources = await WithStoreAsync(
            [server],
            store => store.FindResourcesByScopeAsync(
                ["api1.read", "api1.write"],
                onlyEnabled: true));

        Assert.Contains(resources.Scopes, scope => scope.Name == "api1.read");
        Assert.Contains("api1.write", resources.MissingScopes);
        Assert.False(resources.IsValid);
    }

    [Fact]
    public async Task GetAllEnabledResources_ExcludesDisabledChildScopes()
    {
        var server = Api("api1", Op("api1.read"), Op("api1.write", enabled: false));
        var all = await WithStoreAsync(
            [server],
            store => store.GetAllEnabledResourcesAsync());

        Assert.Contains(all.Scopes, scope => scope.Name == "api1.read");
        Assert.DoesNotContain(all.Scopes, scope => scope.Name == "api1.write");
    }

    [Fact]
    public async Task GetAllEnabledResources_ExcludesDisabledResourceServer()
    {
        var enabled = Api("api1", Op("api1.read"));
        var disabled = Api("api2", Op("api2.read"));
        disabled.Enabled = false;
        var all = await WithStoreAsync(
            [enabled, disabled],
            store => store.GetAllEnabledResourcesAsync());

        Assert.Contains(all.ResourceServers, server => server.Name == "api1");
        Assert.DoesNotContain(all.ResourceServers, server => server.Name == "api2");
    }

    [Fact]
    public async Task Catalog_DuplicateScopeNameAcrossServers_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => WithStoreAsync(
            [Api("api1", Op("shared")), Api("api2", Op("shared"))],
            store => store.GetAllResourcesAsync()));

        Assert.Contains("shared", exception.Message);
    }

    [Fact]
    public async Task FindResourcesByScope_ResourceServerName_IsNotRequestable()
    {
        var resources = await WithStoreAsync(
            [Api("api1", Op("api1.read"))],
            store => store.FindResourcesByScopeAsync(["api1"]));

        Assert.Contains("api1", resources.MissingScopes);
        Assert.DoesNotContain(resources.ResourceServers, server => server.Name == "api1");
        Assert.False(resources.IsValid);
    }

    [Fact]
    public async Task Catalog_DuplicateProtectedResourceUriAcrossServers_Throws()
    {
        var first = Api("api1");
        first.ProtectedResources = [Resource("https://api.example.test/shared")];
        var second = Api("api2");
        second.ProtectedResources = [Resource("https://api.example.test/shared")];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => WithStoreAsync(
            [first, second],
            store => store.GetAllResourcesAsync()));

        Assert.Contains("Duplicate protected resource URI", exception.Message);
    }

    [Theory]
    [InlineData("https://api.example.test/resource#fragment")]
    [InlineData("http://api.example.test/resource")]
    public async Task Catalog_InvalidProtectedResourceUri_Throws(string uri)
    {
        var server = Api("api1");
        server.ProtectedResources = [Resource(uri)];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => WithStoreAsync(
            [server],
            store => store.GetAllResourcesAsync()));

        Assert.Contains("Invalid protected resource URI", exception.Message);
    }

    [Fact]
    public async Task FindRequestedResources_LocalhostHttpProtectedResourceUri_IsAccepted()
    {
        var server = Api("api1");
        server.ProtectedResources = [Resource("http://localhost:5000/resource")];
        var resources = await WithStoreAsync(
            [server],
            store => store.FindRequestedResourcesAsync(
                [],
                ["http://localhost:5000/resource"],
                onlyEnabled: true));

        Assert.False(resources.HasInvalidTargets);
        Assert.Contains(
            resources.ProtectedResources,
            resource => resource.ResourceUri == "http://localhost:5000/resource");
    }

    [Fact]
    public async Task FindRequestedResources_UnknownResource_IsInvalidTarget()
    {
        var server = Api("api1");
        server.ProtectedResources = [Resource("https://api.example.test/resource")];
        var resources = await WithStoreAsync(
            [server],
            store => store.FindRequestedResourcesAsync(
                [],
                ["https://api.example.test/unknown"],
                onlyEnabled: true));

        Assert.True(resources.HasInvalidTargets);
        Assert.Contains("https://api.example.test/unknown", resources.InvalidTargets);
    }

    [Fact]
    public async Task FindRequestedResources_ScopeAndResource_PopulatesOwnerScopeAndProtectedResource()
    {
        var server = Api("api1", Op("api1.read"));
        server.ProtectedResources = [Resource("https://api.example.test/resource")];
        var resources = await WithStoreAsync(
            [server],
            store => store.FindRequestedResourcesAsync(
                ["api1.read"],
                ["https://api.example.test/resource"],
                onlyEnabled: true));

        Assert.True(resources.IsValid);
        Assert.False(resources.HasInvalidTargets);
        Assert.True(resources.IsScopeResourceCoherent());
        Assert.Contains(resources.Scopes, scope => scope.Name == "api1.read");
        Assert.Contains(resources.ResourceServers, resourceServer => resourceServer.Name == "api1");
        Assert.Contains(
            resources.ProtectedResources,
            resource => resource.ResourceUri == "https://api.example.test/resource");
    }

    [Fact]
    public async Task RequestedResources_ScopeResourceCoherence_WhenScopeOwnerResourceIsMissing_ReturnsFalse()
    {
        var first = Api("api1", Op("api1.read"));
        first.ProtectedResources = [Resource("https://api1.example.test/resource")];
        var second = Api("api2", Op("api2.read"));
        second.ProtectedResources = [Resource("https://api2.example.test/resource")];
        var resources = await WithStoreAsync(
            [first, second],
            store => store.FindRequestedResourcesAsync(
                ["api1.read"],
                ["https://api2.example.test/resource"],
                onlyEnabled: true));

        Assert.False(resources.IsScopeResourceCoherent());
    }

    [Fact]
    public async Task RequestedResources_CopyTo_CopiesScopesAndProtectedResources()
    {
        var server = Api("api1", Op("api1.read"));
        server.ProtectedResources = [Resource("https://api.example.test/resource")];
        var source = await WithStoreAsync(
            [server],
            store => store.FindRequestedResourcesAsync(
                ["offline_access", "api1.read"],
                ["https://api.example.test/resource"],
                onlyEnabled: true));
        var target = new RequestedResources();

        source.CopyTo(target);

        Assert.True(target.OfflineAccess);
        Assert.Contains("offline_access", target.RequestedScopeNames);
        Assert.Contains("api1.read", target.RequestedScopeNames);
        Assert.Contains("https://api.example.test/resource", target.RequestedResourceUris);
        Assert.Contains(target.Scopes, scope => scope.Name == "api1.read");
        Assert.Contains(target.ResourceServers, resourceServer => resourceServer.Name == "api1");
        Assert.Contains(
            target.ProtectedResources,
            resource => resource.ResourceUri == "https://api.example.test/resource");
    }
}
