using System.Collections.Concurrent;
using RoyalIdentity.Models.Scopes;

namespace Tests.Integration.Storage;

/// <summary>
/// Unit tests for <see cref="ResourceStore"/> covering the Fase 3 invariants:
/// global scope-name uniqueness and disabled-scope handling.
/// </summary>
public class ResourceStoreTests
{
    private static ConcurrentDictionary<string, ResourceServer> Servers(params ResourceServer[] servers)
    {
        var dict = new ConcurrentDictionary<string, ResourceServer>();
        foreach (var s in servers)
            dict[s.Name] = s;
        return dict;
    }

    private static ConcurrentDictionary<string, IdentityScope> NoIdentityScopes() => new();

    private static ResourceServer Api(string name, params Scope[] scopes)
        => new(ScopeVisibility.Public, name, name, name) { Scopes = [.. scopes] };

    private static Scope Op(string name, bool enabled = true)
        => new(ScopeVisibility.Public, name, name, name) { Enabled = enabled };

    private static ProtectedResource Resource(string uri)
        => new(uri);

    [Fact]
    public async Task FindResourcesByScope_DisabledScope_IsReportedAsInvalid()
    {
        // Collapsed bucket (apontamento 3.1): a disabled scope is reported as invalid (MissingScopes).
        var server = Api("api1", Op("api1.read"), Op("api1.write", enabled: false));
        var store = new ResourceStore(Servers(server), NoIdentityScopes());

        var resources = await store.FindResourcesByScopeAsync(["api1.read", "api1.write"]);

        Assert.Contains(resources.Scopes, s => s.Name == "api1.read");
        Assert.DoesNotContain(resources.Scopes, s => s.Name == "api1.write");
        Assert.Contains("api1.write", resources.MissingScopes);
        Assert.False(resources.IsValid);
    }

    [Fact]
    public async Task FindResourcesByScope_OnlyEnabled_DisabledScope_IsReportedAsInvalid()
    {
        // With onlyEnabled:true (the pipeline path) a disabled scope is also reported as invalid.
        var server = Api("api1", Op("api1.read"), Op("api1.write", enabled: false));
        var store = new ResourceStore(Servers(server), NoIdentityScopes());

        var resources = await store.FindResourcesByScopeAsync(["api1.read", "api1.write"], onlyEnabled: true);

        Assert.Contains(resources.Scopes, s => s.Name == "api1.read");
        Assert.Contains("api1.write", resources.MissingScopes);
        Assert.False(resources.IsValid);
    }

    [Fact]
    public async Task GetAllEnabledResources_ExcludesDisabledChildScopes()
    {
        var server = Api("api1", Op("api1.read"), Op("api1.write", enabled: false));
        var store = new ResourceStore(Servers(server), NoIdentityScopes());

        var all = await store.GetAllEnabledResourcesAsync();

        Assert.Contains(all.Scopes, s => s.Name == "api1.read");
        Assert.DoesNotContain(all.Scopes, s => s.Name == "api1.write");
    }

    [Fact]
    public async Task GetAllEnabledResources_ExcludesDisabledResourceServer()
    {
        var enabled = Api("api1", Op("api1.read"));
        var disabled = Api("api2", Op("api2.read"));
        disabled.Enabled = false;
        var store = new ResourceStore(Servers(enabled, disabled), NoIdentityScopes());

        var all = await store.GetAllEnabledResourcesAsync();

        Assert.Contains(all.ResourceServers, rs => rs.Name == "api1");
        Assert.DoesNotContain(all.ResourceServers, rs => rs.Name == "api2");
    }

    [Fact]
    public void Constructor_DuplicateScopeNameAcrossServers_Throws()
    {
        var a = Api("api1", Op("shared"));
        var b = Api("api2", Op("shared"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => new ResourceStore(Servers(a, b), NoIdentityScopes()));

        Assert.Contains("shared", ex.Message);
    }

    [Fact]
    public async Task FindResourcesByScope_ResourceServerName_IsNotRequestable()
    {
        // ADR-010 / Fase 4 passo 5: a resource server is not requestable by name; only its scopes are.
        var server = Api("api1", Op("api1.read"));
        var store = new ResourceStore(Servers(server), NoIdentityScopes());

        var resources = await store.FindResourcesByScopeAsync(["api1"]);

        Assert.Contains("api1", resources.MissingScopes);
        Assert.DoesNotContain(resources.ResourceServers, rs => rs.Name == "api1");
        Assert.False(resources.IsValid);
    }

    [Fact]
    public void Constructor_DuplicateProtectedResourceUriAcrossServers_Throws()
    {
        var a = WithResources(Api("api1"), "https://api.example.test/shared");
        var b = WithResources(Api("api2"), "https://api.example.test/shared");

        var ex = Assert.Throws<InvalidOperationException>(
            () => new ResourceStore(Servers(a, b), NoIdentityScopes()));

        Assert.Contains("Duplicate protected resource URI", ex.Message);

        static ResourceServer WithResources(ResourceServer server, string uri)
        {
            server.ProtectedResources = [Resource(uri)];
            return server;
        }
    }

    [Fact]
    public void Constructor_ProtectedResourceUriWithFragment_Throws()
    {
        var server = Api("api1");
        server.ProtectedResources = [Resource("https://api.example.test/resource#fragment")];

        var ex = Assert.Throws<InvalidOperationException>(
            () => new ResourceStore(Servers(server), NoIdentityScopes()));

        Assert.Contains("Invalid protected resource URI", ex.Message);
    }

    [Fact]
    public void Constructor_NonHttpsProtectedResourceUri_Throws()
    {
        var server = Api("api1");
        server.ProtectedResources = [Resource("http://api.example.test/resource")];

        var ex = Assert.Throws<InvalidOperationException>(
            () => new ResourceStore(Servers(server), NoIdentityScopes()));

        Assert.Contains("Invalid protected resource URI", ex.Message);
    }

    [Fact]
    public async Task FindRequestedResources_LocalhostHttpProtectedResourceUri_IsAccepted()
    {
        var server = Api("api1");
        server.ProtectedResources = [Resource("http://localhost:5000/resource")];
        var store = new ResourceStore(Servers(server), NoIdentityScopes());

        var resources = await store.FindRequestedResourcesAsync([], ["http://localhost:5000/resource"], onlyEnabled: true);

        Assert.False(resources.HasInvalidTargets);
        Assert.Contains(resources.ProtectedResources, pr => pr.ResourceUri == "http://localhost:5000/resource");
    }

    [Fact]
    public async Task FindRequestedResources_UnknownResource_IsInvalidTarget()
    {
        var server = Api("api1");
        server.ProtectedResources = [Resource("https://api.example.test/resource")];
        var store = new ResourceStore(Servers(server), NoIdentityScopes());

        var resources = await store.FindRequestedResourcesAsync([], ["https://api.example.test/unknown"], onlyEnabled: true);

        Assert.True(resources.HasInvalidTargets);
        Assert.Contains("https://api.example.test/unknown", resources.InvalidTargets);
    }
}
