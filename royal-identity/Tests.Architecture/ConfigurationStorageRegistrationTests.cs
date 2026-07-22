using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Defaults;
using RoyalIdentity.Contracts.Defaults.Jobs;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Storage.EntityFramework.Configuration;
using RoyalIdentity.Storage.EntityFramework.Configuration.Stores;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Architecture;

/// <summary>
/// Verifies the generic DI seam delivered by plan-data-configuration-storage Fase 1. The public
/// registration must use the consumer-selected context with scoped lifetime, perform no implicit database
/// work and never expose the partial production gateway deferred to Plano 3 (DF3/DF6/DF11/DF20).
/// </summary>
public class ConfigurationStorageRegistrationTests
{
	private sealed class CustomConfigurationDbContext(DbContextOptions<CustomConfigurationDbContext> options)
		: DbContext(options)
	{
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.ApplyRoyalIdentityConfigurationSqliteMappings();
		}
	}

	[Fact]
	public void Registration_UsesConsumerContext_WithScopedLifetime_AndNoImplicitDatabaseWork()
	{
		var services = new ServiceCollection();
		services.AddDbContext<CustomConfigurationDbContext>(options =>
			options.UseSqlite("Data Source=:memory:"));
		services.AddEntityFrameworkConfigurationStorage<CustomConfigurationDbContext>();

		using var provider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = true,
			ValidateScopes = true
		});

		CustomConfigurationDbContext firstContext;
		using (var firstScope = provider.CreateScope())
		{
			firstContext = firstScope.ServiceProvider.GetRequiredService<CustomConfigurationDbContext>();
			var firstAccessor = firstScope.ServiceProvider.GetRequiredService<IConfigurationDbContextAccessor>();
			var firstStores = firstScope.ServiceProvider.GetRequiredService<IConfigurationStoreFactory>();

			Assert.Same(firstContext, firstAccessor.DbContext);
			Assert.Same(firstAccessor,
				firstScope.ServiceProvider.GetRequiredService<IConfigurationDbContextAccessor>());
			Assert.Same(firstStores,
				firstScope.ServiceProvider.GetRequiredService<IConfigurationStoreFactory>());
			Assert.Same(firstStores.Realms, firstScope.ServiceProvider.GetRequiredService<IRealmStore>());
			Assert.Equal(ConnectionState.Closed, firstContext.Database.GetDbConnection().State);
		}

		using var secondScope = provider.CreateScope();
		var secondContext = secondScope.ServiceProvider.GetRequiredService<CustomConfigurationDbContext>();
		var secondAccessor = secondScope.ServiceProvider.GetRequiredService<IConfigurationDbContextAccessor>();
		var secondStores = secondScope.ServiceProvider.GetRequiredService<IConfigurationStoreFactory>();

		Assert.Same(secondContext, secondAccessor.DbContext);
		Assert.NotSame(firstContext, secondContext);
		Assert.NotNull(secondStores.Realms);
		Assert.Equal(ConnectionState.Closed, secondContext.Database.GetDbConnection().State);
	}

	[Fact]
	public void Registration_DoesNotProvidePartialStorageGateway()
	{
		var services = new ServiceCollection();
		services.AddDbContext<CustomConfigurationDbContext>(options =>
			options.UseSqlite("Data Source=:memory:"));
		services.AddEntityFrameworkConfigurationStorage<CustomConfigurationDbContext>();

		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		Assert.Null(scope.ServiceProvider.GetService<IStorage>());
		Assert.Null(scope.ServiceProvider.GetService<IStorageProvider>());
		Assert.Null(scope.ServiceProvider.GetService<IStorageSession>());
		Assert.Empty(scope.ServiceProvider.GetServices<IKeyMaterialProtector>());
	}

	[Fact]
	public void Registration_ReturnsSameCollection_AndRejectsNull()
	{
		var services = new ServiceCollection();

		var result = services.AddEntityFrameworkConfigurationStorage<CustomConfigurationDbContext>();

		Assert.Same(services, result);

		IServiceCollection nullServices = null!;
		Assert.Throws<ArgumentNullException>(() =>
			nullServices.AddEntityFrameworkConfigurationStorage<CustomConfigurationDbContext>());
	}

	[Fact]
	public void SnapshotSourceRegistration_RegistersScopedSource_AndNoPartialStorageGateway()
	{
		var services = new ServiceCollection();
		services.AddDbContext<CustomConfigurationDbContext>(options =>
			options.UseSqlite("Data Source=:memory:"));
		services.AddEntityFrameworkConfigurationStorage<CustomConfigurationDbContext>();
		services.AddEntityFrameworkConfigurationSnapshotSource();

		// The EF snapshot source is scoped (it reads the scoped DbContext), so resolving it at the root scope
		// must fail validation — proving it is not a hidden singleton over a captured context.
		using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
		Assert.Throws<InvalidOperationException>(() => provider.GetService<IConfigurationSnapshotSource>());

		using var scope = provider.CreateScope();
		Assert.NotNull(scope.ServiceProvider.GetService<IConfigurationSnapshotSource>());

		// DF20: neither Fase 2 registration provides a partial production gateway.
		Assert.Null(scope.ServiceProvider.GetService<IStorage>());
		Assert.Null(scope.ServiceProvider.GetService<IStorageProvider>());
		Assert.Null(scope.ServiceProvider.GetService<IStorageSession>());
	}

	[Fact]
	public void CoreRegistration_UsesFailFastSigningKeyValidator_AndDoesNotRegisterWriterJob()
	{
		var services = new ServiceCollection();

		services.AddOpenIdConnectProviderServices();

		Assert.Contains(services, descriptor =>
			descriptor.ServiceType == typeof(IHostedService)
			&& descriptor.ImplementationType == typeof(SigningKeyStartupValidator));
		Assert.DoesNotContain(services, descriptor =>
			descriptor.ServiceType == typeof(IServerJob)
			&& descriptor.ImplementationType == typeof(FirstKeyJob));
	}
}
