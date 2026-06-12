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

    [Fact]
    public async Task FindResourcesByScope_DisabledScope_IsReportedAsDisabled()
    {
        var server = Api("api1", Op("api1.read"), Op("api1.write", enabled: false));
        var store = new ResourceStore(Servers(server), NoIdentityScopes());

        var resources = await store.FindResourcesByScopeAsync(["api1.read", "api1.write"]);

        Assert.Contains(resources.Scopes, s => s.Name == "api1.read");
        Assert.DoesNotContain(resources.Scopes, s => s.Name == "api1.write");
        Assert.Contains("api1.write", resources.DisabledScopes);
        Assert.False(resources.IsValid);
    }

    [Fact]
    public async Task FindResourcesByScope_OnlyEnabled_DisabledScope_IsReportedAsMissing()
    {
        // Pins the real pipeline behaviour: every decorator (and the refresh handler) calls with
        // onlyEnabled:true, where a disabled scope is reported as MissingScopes (not DisabledScopes),
        // because the enabled filter short-circuits the if before IsEnabled can classify it.
        var server = Api("api1", Op("api1.read"), Op("api1.write", enabled: false));
        var store = new ResourceStore(Servers(server), NoIdentityScopes());

        var resources = await store.FindResourcesByScopeAsync(["api1.read", "api1.write"], onlyEnabled: true);

        Assert.Contains(resources.Scopes, s => s.Name == "api1.read");
        Assert.Contains("api1.write", resources.MissingScopes);
        Assert.DoesNotContain("api1.write", resources.DisabledScopes);
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
}
