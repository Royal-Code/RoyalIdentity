using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Storage;

public interface IRealmStore
{

    public ValueTask<Realm?> GetByPathAsync(string path, CancellationToken ct);

    public ValueTask<Realm?> GetByIdAsync(string id, CancellationToken ct);

    public ValueTask<Realm?> GetByDomainAsync(string domain, CancellationToken ct = default);

    public IAsyncEnumerable<Realm> GetAllAsync(CancellationToken ct);

    /// <summary>Saves (creates or updates) a realm.</summary>
    ValueTask SaveAsync(Realm realm, CancellationToken ct = default);

    /// <summary>
    /// Deletes a realm by ID. Returns false if not found or if realm is Internal.
    /// </summary>
    ValueTask<bool> DeleteAsync(string realmId, CancellationToken ct = default);
}
