using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Configuration.Resources;
using RoyalIdentity.Storage.EntityFramework.Configuration.Stores;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using Tests.Storage.Configuration.Support;
using Tests.Storage.Support;

namespace Tests.Storage.Configuration;

/// <summary>Provider-specific acceptance tests required by plan Phase 4; these deliberately do not run on the fake.</summary>
public class ConfigurationStoreSqliteTests
{
	[Fact]
	public async Task ServerOptions_ReadsAuthoritativeRow_AndReturnsIndependentGraphs()
	{
		await using var harness = await SqliteConfigurationStorageHarness.CreateConcreteAsync();

		var first = await harness.ConfigurationStores.GetServerOptionsAsync();
		first.IssuerUri = "https://mutated.test";
		var second = await harness.ConfigurationStores.GetServerOptionsAsync();

		Assert.NotSame(first, second);
		Assert.NotEqual("https://mutated.test", second.IssuerUri);
	}

	[Fact]
	public async Task ClientStore_MaterializesCompleteIndependentGraph()
	{
		await using var harness = await SqliteConfigurationStorageHarness.CreateConcreteAsync();
		var source = ConfigurationTestData.BuildFullyPopulatedClient(harness.RealmA, "store-full-client");
		await harness.SeedClientAsync(source);

		var first = await harness.Storage.GetClientStore(harness.RealmA)
			.FindClientByIdAsync(source.Id, default);
		Assert.NotNull(first);
		Assert.Equal(source.Name, first.Name);
		Assert.True(first.AllowedIdentityScopes.SetEquals(source.AllowedIdentityScopes));
		Assert.True(first.AllowedResourceServers.SetEquals(source.AllowedResourceServers));
		Assert.True(first.AllowedScopes.SetEquals(source.AllowedScopes));
		Assert.True(first.RedirectUris.SetEquals(source.RedirectUris));
		Assert.True(first.AllowedCorsOrigins.SetEquals(source.AllowedCorsOrigins));
		Assert.Equal(source.Claims.Select(claim => (claim.Type, claim.Value)),
			first.Claims.Select(claim => (claim.Type, claim.Value)));
		Assert.Equal(source.ClientSecrets.Select(secret => (secret.Type, secret.Value)),
			first.ClientSecrets.Select(secret => (secret.Type, secret.Value)));

		first.Name = "mutated";
		first.AllowedScopes.Add("mutated.scope");
		var second = await harness.Storage.GetClientStore(harness.RealmA)
			.FindClientByIdAsync(source.Id, default);

		Assert.NotNull(second);
		Assert.Equal(source.Name, second.Name);
		Assert.DoesNotContain("mutated.scope", second.AllowedScopes);
	}

	[Fact]
	public async Task DeleteRealm_PreservesConfigurationRows_AndReservesPathAndDomain()
	{
		await using var harness = await SqliteConfigurationStorageHarness.CreateConcreteAsync();
		var realm = await harness.CreateRealmAsync("tombstone");
		await harness.SeedClientAsync(new Client
		{
			Realm = realm,
			Id = "preserved-client",
			Name = "Preserved client",
		});
		harness.DbContext.SigningKeys.Add(new SigningKeyEntity
		{
			RealmId = realm.Id,
			KeyId = "preserved-key",
			Name = "Preserved key",
			SecurityAlgorithm = "RS256",
			SerializationFormat = 0,
			Encoding = 0,
			CreatedUtc = StorageContractHarness.Start,
			ProtectorId = "test",
			ProtectedMaterial = "opaque",
		});
		await harness.DbContext.SaveChangesAsync();
		var optionsJsonBeforeDelete = await harness.DbContext.Realms
			.Where(row => row.Id == realm.Id)
			.Select(row => row.OptionsJson)
			.SingleAsync();

		Assert.True(await harness.Storage.Realms.DeleteAsync(realm.Id));
		harness.DbContext.ChangeTracker.Clear();

		var tombstone = await harness.DbContext.Realms.SingleAsync(row => row.Id == realm.Id);
		Assert.NotNull(tombstone.DeletedAtUtc);
		Assert.Equal(optionsJsonBeforeDelete, tombstone.OptionsJson);
		Assert.True(await harness.DbContext.Clients.AnyAsync(row => row.RealmId == realm.Id));
		Assert.True(await harness.DbContext.SigningKeys.AnyAsync(row => row.RealmId == realm.Id));
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => harness.Storage.Realms.SaveAsync(realm).AsTask());

		var reusedPath = new Realm(
			"reuse-path",
			"new-domain.contract.test",
			realm.Path,
			"Reused path",
			false,
			new RealmOptions(harness.Storage.ServerOptions));
		await Assert.ThrowsAsync<DbUpdateException>(
			() => harness.Storage.Realms.SaveAsync(reusedPath).AsTask());

		harness.DbContext.ChangeTracker.Clear();
		var reusedDomain = new Realm(
			"reuse-domain",
			realm.Domain,
			"new-path",
			"Reused domain",
			false,
			new RealmOptions(harness.Storage.ServerOptions));
		await Assert.ThrowsAsync<DbUpdateException>(
			() => harness.Storage.Realms.SaveAsync(reusedDomain).AsTask());
	}

	[Fact]
	public async Task RealmStore_RejectsNonCanonicalDomain()
	{
		await using var harness = await SqliteConfigurationStorageHarness.CreateConcreteAsync();
		var realm = new Realm(
			"mixed-domain",
			"Mixed.Contract.Test",
			"mixed-domain",
			"Mixed domain",
			false,
			new RealmOptions(harness.Storage.ServerOptions));

		var exception = await Assert.ThrowsAsync<ArgumentException>(
			() => harness.Storage.Realms.SaveAsync(realm).AsTask());

		Assert.Equal("domain", exception.ParamName);
	}

	[Fact]
	public async Task RealmClientAndResourceReads_PropagateCancellation()
	{
		await using var harness = await SqliteConfigurationStorageHarness.CreateConcreteAsync();
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => harness.Storage.Realms.GetByIdAsync(harness.RealmA.Id, cancellation.Token).AsTask());
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => harness.Storage.GetClientStore(harness.RealmA)
				.FindClientByIdAsync("unknown", cancellation.Token));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => harness.Storage.GetResourceStore(harness.RealmA)
				.GetAllResourcesAsync(cancellation.Token));
	}

	[Fact]
	public async Task ResourceBridge_ReturnsIndependentGraphs()
	{
		await using var harness = await SqliteConfigurationStorageHarness.CreateConcreteAsync();
		await harness.SeedIdentityScopeAsync(
			harness.RealmA,
			new IdentityScope(ScopeVisibility.Public, "custom", "Custom", "Custom scope", ["sub"]));

		var first = await harness.Storage.GetResourceStore(harness.RealmA).GetAllResourcesAsync();
		var firstScope = Assert.Single(first.IdentityScopes);
		firstScope.DisplayName = "mutated";
		firstScope.UserClaims.Add("mutated-claim");

		var second = await harness.Storage.GetResourceStore(harness.RealmA).GetAllResourcesAsync();
		var secondScope = Assert.Single(second.IdentityScopes);
		Assert.Equal("Custom", secondScope.DisplayName);
		Assert.DoesNotContain("mutated-claim", secondScope.UserClaims);
	}

	[Fact]
	public void ResourceBridge_ProvidesStandardScopes_AndDemoIsExplicitOptIn()
	{
		var baseServices = CreateResourceServices();
		using var baseProvider = baseServices.BuildServiceProvider();
		var baseSource = baseProvider.GetRequiredService<IConfigurationResourceSource>();

		Assert.Contains(baseSource.GetIdentityScopes("realm"), scope => scope.Name == "openid");
		Assert.Empty(baseSource.GetResourceServers("realm"));

		var demoServices = CreateResourceServices();
		demoServices.AddEntityFrameworkConfigurationDemoResources("demo-realm");
		using var demoProvider = demoServices.BuildServiceProvider();
		var demoSource = demoProvider.GetRequiredService<IConfigurationResourceSource>();

		Assert.Empty(demoSource.GetResourceServers("other-realm"));
		Assert.Contains(demoSource.GetResourceServers("demo-realm"), server => server.Name == "apiserver");
	}

	private static ServiceCollection CreateResourceServices()
	{
		var services = new ServiceCollection();
		services.AddDbContext<ConfigurationSqliteDbContext>(options => options.UseSqlite("Data Source=:memory:"));
		services.AddEntityFrameworkConfigurationStorage<ConfigurationSqliteDbContext>();
		return services;
	}
}
