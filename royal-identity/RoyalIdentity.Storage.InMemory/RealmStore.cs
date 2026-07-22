using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using System.Collections.Concurrent;

namespace RoyalIdentity.Storage.InMemory;

public class RealmStore : IRealmStore
{
    private readonly ConcurrentDictionary<string, Realm> realms;
    private readonly ConcurrentDictionary<string, RealmMemoryStore> realmDataStore;

    public RealmStore(
        ConcurrentDictionary<string, Realm> realms,
        ConcurrentDictionary<string, RealmMemoryStore> realmDataStore)
    {
        this.realms = realms;
        this.realmDataStore = realmDataStore;
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

    public ValueTask SaveAsync(Realm realm, CancellationToken ct = default)
    {
        realms.AddOrUpdate(realm.Id, realm, (_, _) => realm);
        // TryAdd is intentional: on update, preserve existing RealmMemoryStore (tokens/codes/consents).
        // On first save, creates an empty per-realm data container (no pre-loaded clients).
        realmDataStore.TryAdd(realm.Id, new RealmMemoryStore(realm));
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(string realmId, CancellationToken ct = default)
    {
        if (!realms.TryGetValue(realmId, out var realm) || realm.Internal)
            return ValueTask.FromResult(false);

        realms.TryRemove(realmId, out _);
        realmDataStore.TryRemove(realmId, out _);
        return ValueTask.FromResult(true);
    }
}
