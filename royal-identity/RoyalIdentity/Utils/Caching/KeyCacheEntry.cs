using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Keys;

namespace RoyalIdentity.Utils.Caching;

/// <summary>
/// Class for caching keys obtained from the database.
/// </summary>
/// <typeparam name="T">
///     Key type, can be <see cref='SigningCredentials'/> or <see cref='ValidationKeysInfo'/>.
/// </typeparam>
public sealed class KeyCacheEntry<T>
{
    private readonly IStorageProvider storageProvider;
    private readonly string realmId;

    private DateTime expiresAt = DateTime.UtcNow.AddMinutes(-1);
    private IReadOnlyList<string>? ids;
    private T? values;

    public KeyCacheEntry(IStorageProvider storageProvider, string realmId)
    {
        this.storageProvider = storageProvider;
        this.realmId = realmId;
    }

    public bool IsExpired => DateTime.UtcNow > expiresAt;

    public async ValueTask<T> GetOrCreateValue<TArg>(
        Func<TArg, Task<IReadOnlyList<string>>> idsProvider,
        Func<IReadOnlyList<string>, TArg, Task<T>> valuesProvider,
        TArg arg)
    {
        if (IsExpired || ids is null || values is null)
        {
            await Update(idsProvider, valuesProvider, arg);
        }

        return values!;
    }

    public async Task Update<TArg>(
        Func<TArg, Task<IReadOnlyList<string>>> idsProvider,
        Func<IReadOnlyList<string>, TArg, Task<T>> valuesProvider,
        TArg arg)
    {
        var newIds = await idsProvider(arg);

        if (ids is null || values is null || !ids.SequenceEqual(newIds))
        {
            ids = newIds;
            values = await valuesProvider(newIds, arg);
        }

        expiresAt = DateTime.UtcNow.Add(await GetCacheDurationAsync());
    }

    private async Task<TimeSpan> GetCacheDurationAsync()
    {
        using var session = storageProvider.CreateSession();
        var realm = await session.GetStorage().Realms.GetByIdAsync(realmId, CancellationToken.None);
        return realm?.Options.Caching.KeyCacheDuration ?? TimeSpan.FromMinutes(5);
    }
}