using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Configuration;

/// <summary>
/// One immutable, atomically-published configuration snapshot. It owns the materialized graph and never hands
/// it out directly: every read produces a defensive copy (plan DF7, invariante 17). Realm-level options are
/// deep-copied via <see cref="RealmOptions(RealmOptions)"/>; the server options via
/// <see cref="ServerOptions(ServerOptions)"/>.
/// </summary>
internal sealed class PublishedConfigurationSnapshot
{
	private readonly ServerOptions serverOptions;
	private readonly Dictionary<string, Realm> realmsByPath;

	public PublishedConfigurationSnapshot(ConfigurationSnapshotData data, DateTimeOffset loadedAtUtc)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentNullException.ThrowIfNull(data.ServerOptions);
		ArgumentNullException.ThrowIfNull(data.Realms);

		// Take ownership of a fully independent graph. A source may retain its returned objects, but mutating
		// them after publication must never alter the current snapshot.
		serverOptions = new ServerOptions(data.ServerOptions);
		realmsByPath = data.Realms
			.Select(realm => Clone(realm, serverOptions))
			.ToDictionary(realm => realm.Path, StringComparer.Ordinal);
		LoadedAtUtc = loadedAtUtc;
	}

	public DateTimeOffset LoadedAtUtc { get; }

	public IReadOnlyCollection<string> RealmPaths => realmsByPath.Keys;

	public ServerOptions CopyServerOptions() => new(serverOptions);

	public Realm? FindRealmByPath(string path)
		=> realmsByPath.TryGetValue(path, out var realm)
			? Clone(realm, new ServerOptions(serverOptions))
			: null;

	private static Realm Clone(Realm realm, ServerOptions authoritativeServerOptions)
		=> new(realm.Id, realm.Domain, realm.Path, realm.DisplayName, realm.Internal,
			new RealmOptions(realm.Options, authoritativeServerOptions))
		{
			Enabled = realm.Enabled,
		};
}
