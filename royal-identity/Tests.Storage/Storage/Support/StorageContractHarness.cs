using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Options;

namespace Tests.Storage.Support;

/// <summary>
/// <para>
///     Provider-neutral harness for the storage contract suite (plan-data-storage-baseline Fase 3; DF2/DF13).
///     Scenarios only see <see cref="IStorage"/>/<see cref="IStorageProvider"/>, two isolated realms and the
///     test-only seed hooks below — never the concrete backing type. A provider (in-memory today, EF later)
///     plugs in by implementing this harness; the scenarios are reused as-is.
/// </para>
/// <para>
///     Lifecycle is isolated per test: each test creates (and disposes) its own harness, so no state leaks
///     between tests. The seed hooks exist because <see cref="IClientStore"/>/<see cref="IResourceStore"/> are
///     read-only contracts (no write facade yet — DF1); they are test-only and must not become core APIs.
/// </para>
/// </summary>
public abstract class StorageContractHarness : IAsyncDisposable
{
	/// <summary>Deterministic start instant for every harness clock.</summary>
	public static readonly DateTime Start = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

	/// <summary>The storage gateway under test.</summary>
	public abstract IStorage Storage { get; }

	/// <summary>The storage lifetime seam under test (DF21).</summary>
	public abstract IStorageProvider Provider { get; }

	/// <summary>The controllable clock the backing was composed with.</summary>
	public abstract FakeClock Clock { get; }

	/// <summary>First non-internal realm; realm-bound scenarios use colliding ids across A and B (DF6).</summary>
	public abstract Realm RealmA { get; }

	/// <summary>Second non-internal realm, isolated from <see cref="RealmA"/>.</summary>
	public abstract Realm RealmB { get; }

	/// <summary>An internal realm, for the deletion-refusal rule (product: internal realms are not removable).</summary>
	public abstract Realm InternalRealm { get; }

	/// <summary>Seeds a client into its realm (<see cref="Client.Realm"/>). Test-only hook; see class remarks.</summary>
	public abstract Task SeedClientAsync(Client client);

	/// <summary>Seeds an identity scope into the realm's resource configuration. Test-only hook.</summary>
	public abstract Task SeedIdentityScopeAsync(Realm realm, IdentityScope identityScope);

	/// <summary>Seeds a resource server into the realm's resource configuration. Test-only hook.</summary>
	public abstract Task SeedResourceServerAsync(Realm realm, ResourceServer resourceServer);

	public abstract ValueTask DisposeAsync();

	/// <summary>
	/// Creates and persists a new non-internal realm through the public contract (RL-06), so the scenario
	/// exercises the same path a provider must support.
	/// </summary>
	public async Task<Realm> CreateRealmAsync(string suffix)
	{
		var realm = new Realm(
			$"contract-realm-{suffix}",
			$"{suffix}.contract.test",
			$"contract-{suffix}",
			$"Contract Realm {suffix}",
			false,
			new RealmOptions(Storage.ServerOptions));

		await Storage.Realms.SaveAsync(realm);
		return realm;
	}
}
