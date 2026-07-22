using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts;
using Tests.Integration.Prepare;

namespace Tests.Integration.Realm;

/// <summary>
/// End-to-end wiring of the configuration snapshot in the default in-memory host (plan Fase 3, DF7): it is
/// loaded before traffic, a realm created at runtime becomes visible immediately (legacy write requests a
/// reload), and reads hand out defensive copies.
/// </summary>
public class RealmConfigurationSnapshotTests : IClassFixture<AppFactory>
{
	private readonly AppFactory factory;

	public RealmConfigurationSnapshotTests(AppFactory factory)
	{
		this.factory = factory;
		// Force the host (and its hosted services) to start.
		factory.CreateClient();
	}

	[Fact]
	public void Snapshot_IsLoadedAtStartup_WithSeedRealms()
	{
		var snapshot = factory.Services.GetRequiredService<IConfigurationSnapshot>();

		Assert.True(snapshot.IsLoaded);
		Assert.NotNull(snapshot.ServerOptions);
		Assert.NotNull(snapshot.FindRealmByPath("server"));
	}

	[Fact]
	public async Task RealmCreatedAtRuntime_IsVisibleInSnapshot()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var path = $"snap-{suffix}";

		using (var scope = factory.Services.CreateScope())
		{
			var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
			await manager.CreateAsync(path, $"{path}.test", $"Snapshot Realm {suffix}");
		}

		var snapshot = factory.Services.GetRequiredService<IConfigurationSnapshot>();
		Assert.NotNull(snapshot.FindRealmByPath(path));
	}

	[Fact]
	public void ServerOptions_FromSnapshot_IsADefensiveCopy()
	{
		var snapshot = factory.Services.GetRequiredService<IConfigurationSnapshot>();

		var first = snapshot.ServerOptions;
		first.IssuerUri = "https://mutated-by-caller.test";

		Assert.NotEqual("https://mutated-by-caller.test", snapshot.ServerOptions.IssuerUri);
	}
}
