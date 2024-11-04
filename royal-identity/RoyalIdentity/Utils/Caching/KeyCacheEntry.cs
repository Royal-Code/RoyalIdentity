using System.Diagnostics.CodeAnalysis;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models.Keys;

namespace RoyalIdentity.Utils.Caching;

/// <summary>
/// Class for caching keys obtained from the database.
/// </summary>
/// <typeparam name="T">
///     Key type, can be <see cref=‘SigningCredentials’/> or <see cref=‘SecurityKeyInfo’/>.
/// </typeparam>
public sealed class KeyCacheEntry<T>
{
    private readonly TimeSpan cacheDuration;

    private DateTime expiresAt = DateTime.UtcNow.AddMinutes(-1);
    private IReadOnlyList<string>? ids;
    private IReadOnlyList<T>? values;

    public KeyCacheEntry(TimeSpan? cacheDuration = null)
    {
        this.cacheDuration = cacheDuration ?? TimeSpan.FromMinutes(5);
    }

    public bool IsExpired => DateTime.UtcNow > expiresAt;

    public async ValueTask<IReadOnlyList<T>> GetOrCreateValue(
        [NotNull] Func<CancellationToken, Task<IReadOnlyList<string>>> idsProvider,
        [NotNull] Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<T>>> valuesProvider,
        CancellationToken ct)
    {
        if (IsExpired || ids is null || values is null)
        {
            await Update(idsProvider, valuesProvider, ct);
        }

        return values!;
    }

    public async Task Update(
        [NotNull] Func<CancellationToken, Task<IReadOnlyList<string>>> idsProvider,
        [NotNull] Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<T>>> valuesProvider,
        CancellationToken ct)
    {
        var newIds = await idsProvider(ct);

        if (ids is null || values is null || !ids.SequenceEqual(newIds))
        {
            ids = newIds;
            values = await valuesProvider(newIds, ct);
        }

        expiresAt = DateTime.UtcNow.Add(cacheDuration);
    }
}