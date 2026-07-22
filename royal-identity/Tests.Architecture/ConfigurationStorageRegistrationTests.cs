using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Storage.EntityFramework.Configuration;
using RoyalIdentity.Storage.EntityFramework.Extensions;
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

			Assert.Same(firstContext, firstAccessor.DbContext);
			Assert.Same(firstAccessor,
				firstScope.ServiceProvider.GetRequiredService<IConfigurationDbContextAccessor>());
			Assert.Equal(ConnectionState.Closed, firstContext.Database.GetDbConnection().State);
		}

		using var secondScope = provider.CreateScope();
		var secondContext = secondScope.ServiceProvider.GetRequiredService<CustomConfigurationDbContext>();
		var secondAccessor = secondScope.ServiceProvider.GetRequiredService<IConfigurationDbContextAccessor>();

		Assert.Same(secondContext, secondAccessor.DbContext);
		Assert.NotSame(firstContext, secondContext);
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
}
