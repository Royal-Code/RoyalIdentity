using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Configuration;

/// <summary>
/// <para>
///     A synchronous, singleton view over the already-published configuration state (plan DF7). It exists so
///     the few synchronous consumers of configuration (cookie options, event dispatch, realm creation, the
///     check-session page) never touch storage on their hot path and never open a database connection
///     synchronously. The bootstrap and periodic refresh are asynchronous and live in
///     <see cref="IConfigurationSnapshotSource"/> plus the hosted refresher.
/// </para>
/// <para>
///     The snapshot never exposes its internal mutable graph: <see cref="ServerOptions"/> and
///     <see cref="FindRealmByPath"/> return defensive copies, so mutating a returned object cannot change a
///     later read or the published state (invariante 17).
/// </para>
/// </summary>
public interface IConfigurationSnapshot
{
	/// <summary>Whether an initial snapshot has been published. Reads throw until it is.</summary>
	bool IsLoaded { get; }

	/// <summary>A defensive copy of the authoritative server options.</summary>
	ServerOptions ServerOptions { get; }

	/// <summary>A defensive copy of the realm with the given path, or <c>null</c> when unknown/tombstoned.</summary>
	Realm? FindRealmByPath(string path);

	/// <summary>The paths of the realms in the current snapshot (used to scope named-options invalidation).</summary>
	IReadOnlyCollection<string> RealmPaths { get; }

	/// <summary>When the current snapshot was published (for observable staleness — plan DF26).</summary>
	DateTimeOffset LoadedAtUtc { get; }

	/// <summary>When the last periodic refresh failed, if any; a successful publish clears it (plan DF26).</summary>
	DateTimeOffset? LastRefreshFailureUtc { get; }
}
