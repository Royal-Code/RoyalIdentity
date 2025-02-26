using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;

namespace RoyalIdentity.Storage.InMemory;

public class RealmStore : IRealmStore
{
    private readonly MemoryStorage storage;

    public RealmStore(MemoryStorage storage)
    {
        this.storage = storage;
    }

    public ValueTask<Realm?> GetByPathAsync(string path, CancellationToken ct)
    {
        var realm = storage.Reamls.Values.FirstOrDefault(r => r.Path == path);

        return new ValueTask<Realm?>(realm);
    }
}
