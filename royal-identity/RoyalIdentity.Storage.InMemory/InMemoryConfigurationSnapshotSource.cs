using RoyalIdentity.Configuration;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Storage.InMemory;

/// <summary>
/// <see cref="IConfigurationSnapshotSource"/> backed by the in-memory fake (default host). It reads the fake's
/// own server options and realms directly — never through <c>IStorage.ServerOptions</c> — and materializes an
/// independent graph (a server-options copy plus cloned realms), so the published snapshot cannot be mutated
/// through the fake and vice-versa (plan DF7).
/// </summary>
internal sealed class InMemoryConfigurationSnapshotSource(MemoryStorage storage) : IConfigurationSnapshotSource
{
	public Task<ConfigurationSnapshotData> LoadAsync(CancellationToken ct)
	{
		var serverOptions = new ServerOptions(storage.ServerOptions);

		var realms = storage.Realms.Values
			.Select(realm => Clone(realm, serverOptions))
			.ToList();

		return Task.FromResult(new ConfigurationSnapshotData
		{
			ServerOptions = serverOptions,
			Realms = realms,
		});
	}

	private static Realm Clone(Realm realm, ServerOptions authoritativeServerOptions)
		=> new(realm.Id, realm.Domain, realm.Path, realm.DisplayName, realm.Internal,
			new RealmOptions(realm.Options, authoritativeServerOptions))
		{
			Enabled = realm.Enabled,
		};
}
