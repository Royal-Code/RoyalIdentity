using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Scopes;
using Tests.Storage.Support;
using static RoyalIdentity.Options.Constants;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IResourceStore</c> and its extension (matrix RS-01..RS-05). The resources/scopes shape is
/// blocked for persistence by the redesign (DF11/DF22), but every `preservar` rule is locked provider-neutrally:
/// the enabled filter (RS-02, ADR-010), realm isolation (DF6), the request-resolution semantics (RS-04,
/// ADR-010/012) and the authorized-subset resolution (RS-05, ADR-012/RFC 8707). Construction-consistency rules
/// of the in-memory index (duplicate scope/URI, malformed URI) remain in
/// <c>Tests.Integration/Storage/ResourceStoreTests</c> as implementation coverage of the current backing.
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

	// DF18: scope names and resource URIs compare literally/Ordinal; provider collation must not turn
	// differently-cased protocol values into matches.
	[Fact]
	public async Task FindRequestedResources_ScopeOrResourceDifferingOnlyByCase_IsNotResolved()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.SeedIdentityScopeAsync(harness.RealmA, NewIdentityScope("contract:case-scope"));
		var server = NewResourceServer("contract:case-server");
		server.ProtectedResources = [new ProtectedResource("https://contract-case.api.test/Resource")];
		await harness.SeedResourceServerAsync(harness.RealmA, server);

		var resources = await harness.Storage.GetResourceStore(harness.RealmA).FindRequestedResourcesAsync(
			["CONTRACT:CASE-SCOPE"], ["https://contract-case.api.test/resource"], onlyEnabled: true, default);

		Assert.Contains("CONTRACT:CASE-SCOPE", resources.MissingScopes);
		Assert.Contains("https://contract-case.api.test/resource", resources.InvalidTargets);
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

	// RS-04 `preservar` (ADR-010/012): a scope plus its authorized resource indicator resolve to the owning
	// server, the scope and the protected resource, producing a valid and coherent request.
	[Fact]
	public async Task FindRequestedResources_ScopeAndResource_ResolvesOwnerScopeAndProtectedResource()
	{
		await using var harness = await CreateHarnessAsync();
		var server = NewResourceServer("contract:rs4", enabled: true, NewScope("contract:rs4.read"));
		server.ProtectedResources = [new ProtectedResource("https://contract-rs4.api.test/resource")];
		await harness.SeedResourceServerAsync(harness.RealmA, server);

		var resources = await harness.Storage.GetResourceStore(harness.RealmA).FindRequestedResourcesAsync(
			["contract:rs4.read"], ["https://contract-rs4.api.test/resource"], onlyEnabled: true, default);

		Assert.True(resources.IsValid);
		Assert.False(resources.HasInvalidTargets);
		Assert.True(resources.IsScopeResourceCoherent());
		Assert.Contains(resources.Scopes, s => s.Name == "contract:rs4.read");
		Assert.Contains(resources.ResourceServers, rs => rs.Name == "contract:rs4");
		Assert.Contains(resources.ProtectedResources, pr => pr.ResourceUri == "https://contract-rs4.api.test/resource");
	}

	// RS-04 `preservar` (ADR-010): unknown scope names are reported as invalid, never resolved silently.
	[Fact]
	public async Task FindRequestedResources_UnknownScope_IsReportedInvalid()
	{
		await using var harness = await CreateHarnessAsync();

		var resources = await harness.Storage.GetResourceStore(harness.RealmA).FindRequestedResourcesAsync(
			["contract:unknown-scope"], [], onlyEnabled: true, default);

		Assert.False(resources.IsValid);
		Assert.Contains("contract:unknown-scope", resources.MissingScopes);
	}

	// RS-04 `preservar` (ADR-010, bucket único): a disabled scope requested on the enabled path is reported
	// as invalid together with unknown scopes.
	[Fact]
	public async Task FindRequestedResources_DisabledScope_IsReportedInvalid()
	{
		await using var harness = await CreateHarnessAsync();
		await harness.SeedResourceServerAsync(harness.RealmA,
			NewResourceServer("contract:rs4d", enabled: true, NewScope("contract:rs4d.off", enabled: false)));

		var resources = await harness.Storage.GetResourceStore(harness.RealmA).FindRequestedResourcesAsync(
			["contract:rs4d.off"], [], onlyEnabled: true, default);

		Assert.False(resources.IsValid);
		Assert.Contains("contract:rs4d.off", resources.MissingScopes);
	}

	// RS-04 `preservar` (ADR-012/RFC 8707): unknown resource URIs and URIs of disabled resource servers are
	// reported as invalid targets.
	[Fact]
	public async Task FindRequestedResources_UnknownOrDisabledResourceUri_IsInvalidTarget()
	{
		await using var harness = await CreateHarnessAsync();
		var disabledServer = NewResourceServer("contract:rs4off", enabled: false);
		disabledServer.ProtectedResources = [new ProtectedResource("https://contract-rs4off.api.test/resource")];
		await harness.SeedResourceServerAsync(harness.RealmA, disabledServer);

		var resources = await harness.Storage.GetResourceStore(harness.RealmA).FindRequestedResourcesAsync(
			[],
			["https://contract-unknown.api.test/resource", "https://contract-rs4off.api.test/resource"],
			onlyEnabled: true, default);

		Assert.True(resources.HasInvalidTargets);
		Assert.Contains("https://contract-unknown.api.test/resource", resources.InvalidTargets);
		Assert.Contains("https://contract-rs4off.api.test/resource", resources.InvalidTargets);
	}

	// RS-04 `preservar` (ADR-012): relative resource indicators and absolute URIs with fragments are malformed
	// protocol input and must be reported as invalid targets by every provider.
	[Fact]
	public async Task FindRequestedResources_RelativeOrFragmentResourceUri_IsInvalidTarget()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.GetResourceStore(harness.RealmA);

		var resources = await store.FindRequestedResourcesAsync(
			[], ["contract-relative-resource", "https://contract.api.test/resource#fragment"],
			onlyEnabled: true, default);

		Assert.True(resources.HasInvalidTargets);
		Assert.Contains("contract-relative-resource", resources.InvalidTargets);
		Assert.Contains("https://contract.api.test/resource#fragment", resources.InvalidTargets);
	}

	// RS-04 `preservar` (ADR-012/local policy): plain HTTP is not accepted for a non-loopback resource URI.
	[Fact]
	public async Task FindRequestedResources_HttpNonLoopbackResourceUri_IsInvalidTarget()
	{
		await using var harness = await CreateHarnessAsync();

		var resources = await harness.Storage.GetResourceStore(harness.RealmA).FindRequestedResourcesAsync(
			[], ["http://contract.api.test/resource"], onlyEnabled: true, default);

		Assert.True(resources.HasInvalidTargets);
		Assert.Contains("http://contract.api.test/resource", resources.InvalidTargets);
	}

	// RS-04 `preservar` (local development policy): HTTP remains valid for a loopback protected resource.
	[Fact]
	public async Task FindRequestedResources_HttpLocalhostResourceUri_IsAccepted()
	{
		await using var harness = await CreateHarnessAsync();
		var server = NewResourceServer("contract:rs4local");
		server.ProtectedResources = [new ProtectedResource("http://localhost:5100/resource")];
		await harness.SeedResourceServerAsync(harness.RealmA, server);

		var resources = await harness.Storage.GetResourceStore(harness.RealmA).FindRequestedResourcesAsync(
			[], ["http://localhost:5100/resource"], onlyEnabled: true, default);

		Assert.False(resources.HasInvalidTargets);
		Assert.Contains(resources.ProtectedResources,
			resource => resource.ResourceUri == "http://localhost:5100/resource");
	}

	// RS-05 `preservar` (ADR-012/RFC 8707): a requested resource indicator outside the previously authorized
	// set fails with invalid_target, reporting the offending URI.
	[Fact]
	public async Task ResolveAuthorizedSubset_ResourceOutsideAuthorizedSet_FailsWithInvalidTarget()
	{
		await using var harness = await CreateHarnessAsync();
		var server = NewResourceServer("contract:rs5", enabled: true, NewScope("contract:rs5.read"));
		server.ProtectedResources = [new ProtectedResource("https://contract-rs5.api.test/authorized")];
		await harness.SeedResourceServerAsync(harness.RealmA, server);

		var resolution = await harness.Storage.GetResourceStore(harness.RealmA).ResolveAuthorizedSubsetAsync(
			["contract:rs5.read"],
			["https://contract-rs5.api.test/authorized"],
			["https://contract-rs5.api.test/not-authorized"],
			onlyEnabled: true, default);

		Assert.False(resolution.IsSuccess);
		Assert.Equal(Oidc.Token.Errors.InvalidTarget, resolution.Error);
		Assert.NotNull(resolution.Detail);
		Assert.Contains("https://contract-rs5.api.test/not-authorized", resolution.Detail);
	}

	// RS-05 `preservar` (ADR-012): without requested resource indicators, the full authorized set is resolved.
	[Fact]
	public async Task ResolveAuthorizedSubset_WithoutRequestedResources_UsesFullAuthorizedSet()
	{
		await using var harness = await CreateHarnessAsync();
		var server = NewResourceServer("contract:rs5full", enabled: true, NewScope("contract:rs5full.read"));
		server.ProtectedResources = [new ProtectedResource("https://contract-rs5full.api.test/resource")];
		await harness.SeedResourceServerAsync(harness.RealmA, server);

		var resolution = await harness.Storage.GetResourceStore(harness.RealmA).ResolveAuthorizedSubsetAsync(
			["contract:rs5full.read"],
			["https://contract-rs5full.api.test/resource"],
			[],
			onlyEnabled: true, default);

		Assert.True(resolution.IsSuccess);
		Assert.NotNull(resolution.Resources);
		Assert.Contains(resolution.Resources.Scopes, s => s.Name == "contract:rs5full.read");
		Assert.Contains(resolution.Resources.ProtectedResources,
			pr => pr.ResourceUri == "https://contract-rs5full.api.test/resource");
	}

	// RS-05 `preservar` (ADR-012/RFC 8707): requesting a subset of the authorized resources downscopes the
	// grant coherently — scopes of the non-selected resource server are dropped, not failed.
	[Fact]
	public async Task ResolveAuthorizedSubset_SubsetRequest_DownscopesScopesToSelectedResource()
	{
		await using var harness = await CreateHarnessAsync();
		var serverA = NewResourceServer("contract:rs7a", enabled: true, NewScope("contract:rs7a.read"));
		serverA.ProtectedResources = [new ProtectedResource("https://contract-rs7a.api.test/resource")];
		var serverB = NewResourceServer("contract:rs7b", enabled: true, NewScope("contract:rs7b.read"));
		serverB.ProtectedResources = [new ProtectedResource("https://contract-rs7b.api.test/resource")];
		await harness.SeedResourceServerAsync(harness.RealmA, serverA);
		await harness.SeedResourceServerAsync(harness.RealmA, serverB);

		var resolution = await harness.Storage.GetResourceStore(harness.RealmA).ResolveAuthorizedSubsetAsync(
			["contract:rs7a.read", "contract:rs7b.read"],
			["https://contract-rs7a.api.test/resource", "https://contract-rs7b.api.test/resource"],
			["https://contract-rs7a.api.test/resource"],
			onlyEnabled: true, default);

		Assert.True(resolution.IsSuccess);
		Assert.NotNull(resolution.Resources);
		Assert.Contains(resolution.Resources.Scopes, s => s.Name == "contract:rs7a.read");
		Assert.DoesNotContain(resolution.Resources.Scopes, s => s.Name == "contract:rs7b.read");
		var resource = Assert.Single(resolution.Resources.ProtectedResources);
		Assert.Equal("https://contract-rs7a.api.test/resource", resource.ResourceUri);
	}

	public sealed class InMemory : ResourceStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync() => InMemoryStorageHarness.CreateAsync();
	}
}
