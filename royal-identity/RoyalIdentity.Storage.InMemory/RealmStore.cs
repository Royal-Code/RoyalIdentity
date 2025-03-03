using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using System.Collections.Concurrent;

namespace RoyalIdentity.Storage.InMemory;

public class RealmStore : IRealmStore
{
    private readonly ConcurrentDictionary<string, Realm> realms;

    public RealmStore(ConcurrentDictionary<string, Realm> realms)
    {
        this.realms = realms;
    }

    public async IAsyncEnumerable<Realm> GetAllAsync(CancellationToken ct)
    {
        foreach (var realm in realms.Values)
        {
            if (ct.IsCancellationRequested)
                yield break;
            yield return realm;
        }
    }

    public ValueTask<Realm?> GetByDomainAsync(string domain, CancellationToken ct = default)
    {
        var realm = realms.Values.FirstOrDefault(r => r.Domain == domain);
        return new ValueTask<Realm?>(realm);
    }

    public ValueTask<Realm?> GetByIdAsync(string id, CancellationToken ct)
    {
        realms.TryGetValue(id, out var realm);
        return new ValueTask<Realm?>(realm);
    }

    public ValueTask<Realm?> GetByPathAsync(string path, CancellationToken ct)
    {
        var realm = realms.Values.FirstOrDefault(r => r.Path == path);

        return new ValueTask<Realm?>(realm);
    }
}
