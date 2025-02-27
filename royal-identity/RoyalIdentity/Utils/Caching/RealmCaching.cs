using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using System.Collections.Concurrent;

namespace RoyalIdentity.Utils.Caching;

/// <summary>
/// Service class for caching multiple models per realm.
/// </summary>
public sealed class RealmCaching
{
    private readonly IStorageProvider storageProvider;
    private readonly ConcurrentDictionary<string, KeyCache> keyCaches = new();

    public RealmCaching(IStorageProvider storageProvider)
    {
        this.storageProvider = storageProvider;
    }

    public KeyCache GetKeyCache(Realm realm)
    {
        return keyCaches.GetOrAdd(realm.Id, static (id, sp) => new KeyCache(sp, id), storageProvider);
    }
}