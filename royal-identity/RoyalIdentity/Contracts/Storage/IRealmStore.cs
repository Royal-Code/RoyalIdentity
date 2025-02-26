using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Storage;

public interface IRealmStore
{

    public ValueTask<Realm?> GetByPathAsync(string path, CancellationToken ct);
}
