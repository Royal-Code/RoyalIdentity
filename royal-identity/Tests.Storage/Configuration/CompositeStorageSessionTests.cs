using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using RoyalIdentity.Storage.InMemory.Extensions;
using Tests.Storage.Configuration.Support;
using Tests.Storage.Support;

namespace Tests.Storage.Configuration;

/// <summary>
/// The test-only composite lifecycle (plan Fase 3, DF6/DF20): a storage session owns a fresh scope combining
/// the Configuration EF context and the Operational in-memory storage, each session gets an independent scope,
/// and disposing the session releases its scoped dependencies — with no behavior surviving after disposal.
/// </summary>
public class CompositeStorageSessionTests
{
	private static ServiceProvider BuildProvider(ScopedDisposalTracker tracker)
	{
		var services = new ServiceCollection();
		services.AddSingleton<TimeProvider>(new FakeClock(StorageContractHarness.Start));
		services.AddSingleton(tracker);
		services.AddScoped<ScopedProbe>();

		services.AddDbContext<ConfigurationSqliteDbContext>(options => options.UseSqlite("Data Source=:memory:"));
		services.AddEntityFrameworkConfigurationStorage<ConfigurationSqliteDbContext>();
		services.AddInMemoryStorage();

		// Test-only composite provider replaces the in-memory provider registered above (last wins).
		services.AddSingleton<IStorageProvider, CompositeStorageProvider>();

		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
	}

	[Fact]
	public void Session_CombinesConfigurationAndOperational_AndDisposalReleasesScopedDependency()
	{
		var tracker = new ScopedDisposalTracker();
		using var provider = BuildProvider(tracker);
		var storageProvider = provider.GetRequiredService<IStorageProvider>();

		var session = (CompositeStorageSession)storageProvider.CreateSession();

		Assert.NotNull(session.GetStorage());            // Operational (in-memory) available in the scope
		Assert.NotNull(session.ConfigurationDbContext);  // Configuration (EF) owned by the same scope
		Assert.Equal(0, tracker.DisposedCount);

		session.Dispose();

		Assert.Equal(1, tracker.DisposedCount);
		// No behavior survives after disposal: the scope is gone, so the storage can no longer be resolved.
		Assert.Throws<ObjectDisposedException>(() => session.GetStorage());
	}

	[Fact]
	public void EachSession_OwnsAnIndependentScope()
	{
		var tracker = new ScopedDisposalTracker();
		using var provider = BuildProvider(tracker);
		var storageProvider = provider.GetRequiredService<IStorageProvider>();

		var first = (CompositeStorageSession)storageProvider.CreateSession();
		var second = (CompositeStorageSession)storageProvider.CreateSession();

		Assert.NotSame(first.ConfigurationDbContext, second.ConfigurationDbContext);

		first.Dispose();
		second.Dispose();

		Assert.Equal(2, tracker.DisposedCount);
	}
}
