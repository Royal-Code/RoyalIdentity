using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IResourceStore</c> (matrix RS-01..RS-05) at baseline scope: the resources/scopes shape is
/// blocked for persistence by the redesign (DF11/DF22), so only the rules consumers already rely on are
/// locked — the enabled filter (RS-02, ADR-010) and realm isolation (DF6). The full request-resolution
/// semantics (RS-04/RS-05) remain covered by <c>Tests.Integration/Storage/ResourceStoreTests</c>; duplicating
/// them here would add no contract (Fase 3: no duplicated coverage).
/// </summary>
public abstract class ResourceStoreContractTests : StorageContractTests
{
	// RS-02 `preservar` (ADR-010): disabled identity scopes, disabled resource servers and disabled scopes
	// of enabled servers are all excluded from the enabled snapshot.
	[Fact]
	public async Task GetAllEnabledResources_ExcludesDisabledIdentityScopesServersAndScopes()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.SeedIdentityScopeAsync(harness.RealmA, NewIdentityScope("contract:id-on"));
		await harness.SeedIdentityScopeAsync(harness.RealmA, NewIdentityScope("contract:id-off", enabled: false));
		await harness.SeedResourceServerAsync(harness.RealmA,
			NewResourceServer("contract:rs-off", enabled: false, NewScope("contract:s-of-off")));
		await harness.SeedResourceServerAsync(harness.RealmA,
			NewResourceServer("contract:rs-on", enabled: true,
				NewScope("contract:s-on"), NewScope("contract:s-off", enabled: false)));

		var all = await harness.Storage.GetResourceStore(harness.RealmA).GetAllEnabledResourcesAsync(default);

		Assert.Contains(all.IdentityScopes, s => s.Name == "contract:id-on");
		Assert.DoesNotContain(all.IdentityScopes, s => s.Name == "contract:id-off");
		Assert.DoesNotContain(all.ResourceServers, rs => rs.Name == "contract:rs-off");

		var enabledServer = Assert.Single(all.ResourceServers, rs => rs.Name == "contract:rs-on");
		Assert.Contains(enabledServer.Scopes, s => s.Name == "contract:s-on");
		Assert.DoesNotContain(enabledServer.Scopes, s => s.Name == "contract:s-off");
	}

	// RS-03 + DF6: a scope configured in one realm resolves only through that realm's store.
	[Fact]
	public async Task FindResourcesByScope_ResolvesScopeOnlyInItsOwnRealm()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.SeedIdentityScopeAsync(harness.RealmA, NewIdentityScope("contract:iso-scope"));

		var inA = await harness.Storage.GetResourceStore(harness.RealmA)
			.FindResourcesByScopeAsync(["contract:iso-scope"], onlyEnabled: true, default);
		var inB = await harness.Storage.GetResourceStore(harness.RealmB)
			.FindResourcesByScopeAsync(["contract:iso-scope"], onlyEnabled: true, default);

		Assert.Contains(inA.IdentityScopes, s => s.Name == "contract:iso-scope");
		Assert.DoesNotContain(inB.IdentityScopes, s => s.Name == "contract:iso-scope");
	}

	// DF6: the same scope name in two realms resolves to each realm's own configuration.
	[Fact]
	public async Task SameScopeName_InTwoRealms_ResolvesToEachRealmsOwnConfiguration()
	{
		await using var harness = await CreateHarnessAsync();

		var scopeInA = NewIdentityScope("contract:shared-scope");
		scopeInA.UserClaims.Add("claim-of-a");
		var scopeInB = NewIdentityScope("contract:shared-scope");
		scopeInB.UserClaims.Add("claim-of-b");

		await harness.SeedIdentityScopeAsync(harness.RealmA, scopeInA);
		await harness.SeedIdentityScopeAsync(harness.RealmB, scopeInB);

		var inA = await harness.Storage.GetResourceStore(harness.RealmA)
			.FindResourcesByScopeAsync(["contract:shared-scope"], onlyEnabled: true, default);
		var inB = await harness.Storage.GetResourceStore(harness.RealmB)
			.FindResourcesByScopeAsync(["contract:shared-scope"], onlyEnabled: true, default);

		var resolvedInA = Assert.Single(inA.IdentityScopes, s => s.Name == "contract:shared-scope");
		var resolvedInB = Assert.Single(inB.IdentityScopes, s => s.Name == "contract:shared-scope");
		Assert.Contains("claim-of-a", resolvedInA.UserClaims);
		Assert.Contains("claim-of-b", resolvedInB.UserClaims);
	}

	public sealed class InMemory : ResourceStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync() => InMemoryStorageHarness.CreateAsync();
	}
}
