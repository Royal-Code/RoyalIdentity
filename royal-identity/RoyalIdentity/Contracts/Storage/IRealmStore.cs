using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Storage;

public interface IRealmStore
{

    public ValueTask<Realm?> GetByPathAsync(string path, CancellationToken ct);

    public ValueTask<Realm?> GetByIdAsync(string id, CancellationToken ct);

    public ValueTask<Realm?> GetByDomainAsync(string domain, CancellationToken ct = default);

    public IAsyncEnumerable<Realm> GetAllAsync(CancellationToken ct);
}
