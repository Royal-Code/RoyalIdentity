using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Configuration;
using RoyalIdentity.Data.Configuration;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using Tests.Storage.Configuration.Support;

namespace Tests.Storage.Configuration;

/// <summary>
/// The EF <see cref="IConfigurationSnapshotSource"/> over a real SQLite database (plan Fase 3, DF7): it reads
/// the authoritative server options and the live (non-tombstoned) realms, materializes an independent graph,
/// and is fail-closed when the singleton server-options row is missing. It reads through the scoped context
/// accessor, never through <c>IStorage</c>.
/// </summary>
public abstract class ConfigurationSnapshotSourceProviderTests<TContext>
	where TContext : ConfigurationDbContext
{
	private static readonly ServerOptionsPayloadSerializer ServerSerializer = new();
	private static readonly RealmOptionsPayloadSerializer RealmSerializer = new();

	private protected abstract Task<IConfigurationTestDatabase<TContext>> CreateDatabaseAsync();

	[Fact]
	public async Task LoadAsync_MaterializesServerOptionsAndLiveRealms_ExcludingTombstones()
	{
		await using var database = await CreateDatabaseAsync();
		var serverOptions = new ServerOptions { IssuerUri = "https://issuer.test", DispatchEvents = true };

		await using (var context = database.NewContext())
		{
			context.ServerOptions.Add(BuildServerRow(serverOptions));
			context.Realms.Add(BuildRealmRow("alpha", serverOptions, deleted: false));
			context.Realms.Add(BuildRealmRow("beta", serverOptions, deleted: false));
			context.Realms.Add(BuildRealmRow("gone", serverOptions, deleted: true));
			await context.SaveChangesAsync();
		}

		var data = await LoadAsync(database);

		Assert.Equal("https://issuer.test", data.ServerOptions.IssuerUri);
		Assert.True(data.ServerOptions.DispatchEvents);

		var paths = data.Realms.Select(r => r.Path).ToHashSet();
		Assert.Equal(["alpha", "beta"], paths); // tombstoned "gone" excluded
		Assert.All(data.Realms, r => Assert.Same(data.ServerOptions, r.Options.ServerOptions));
	}

	[Fact]
	public async Task LoadAsync_WhenServerOptionsRowMissing_FailsClosed()
	{
		await using var database = await CreateDatabaseAsync();

		await Assert.ThrowsAsync<InvalidOperationException>(() => LoadAsync(database).AsTask());
	}

	private static async ValueTask<ConfigurationSnapshotData> LoadAsync(
		IConfigurationTestDatabase<TContext> database)
	{
		var services = new ServiceCollection();
		database.AddStorage(services);
		services.AddEntityFrameworkConfigurationSnapshotSource();

		await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
		using var scope = provider.CreateScope();
		var source = scope.ServiceProvider.GetRequiredService<IConfigurationSnapshotSource>();

		return await source.LoadAsync(CancellationToken.None);
	}

	private static ServerOptionsEntity BuildServerRow(ServerOptions serverOptions)
	{
		var (version, json) = ServerSerializer.Serialize(serverOptions);
		return new ServerOptionsEntity
		{
			Id = ServerOptionsEntity.SingletonId,
			PayloadVersion = version,
			PayloadJson = json,
			UpdatedAtUtc = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc),
		};
	}

	private static RealmEntity BuildRealmRow(string path, ServerOptions serverOptions, bool deleted)
	{
		var (version, json) = RealmSerializer.Serialize(new RealmOptions(serverOptions));
		return new RealmEntity
		{
			Id = path,
			Path = path,
			Domain = $"{path}.test",
			DisplayName = $"Realm {path}",
			Enabled = true,
			Internal = false,
			OptionsVersion = version,
			OptionsJson = json,
			DeletedAtUtc = deleted ? new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc) : null,
		};
	}
}

public sealed class ConfigurationSnapshotSourceSqliteTests
	: ConfigurationSnapshotSourceProviderTests<ConfigurationSqliteDbContext>
{
	private protected override async Task<IConfigurationTestDatabase<ConfigurationSqliteDbContext>>
		CreateDatabaseAsync()
		=> await SqliteConfigurationDatabase.CreateMigratedAsync();
}
