using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Stores;

/// <summary>
/// Scoped entry point for the Configuration-only stores implemented by this adapter. It deliberately does
/// not implement <see cref="IStorage"/>: the complete production gateway is composed only after the
/// Operational family exists (plan DF20).
/// </summary>
public interface IConfigurationStoreFactory
{
	/// <summary>Reads and materializes the single authoritative server-options row.</summary>
	Task<ServerOptions> GetServerOptionsAsync(CancellationToken ct = default);

	/// <summary>Gets the global realm store.</summary>
	IRealmStore Realms { get; }

	/// <summary>Creates a database-backed, realm-bound client store.</summary>
	IClientStore GetClientStore(Realm realm);

	/// <summary>Creates a database-backed, realm-bound signing-key store.</summary>
	IKeyStore GetKeyStore(Realm realm);

	/// <summary>Creates the transitional volatile, realm-bound resource bridge.</summary>
	IResourceStore GetResourceStore(Realm realm);
}
