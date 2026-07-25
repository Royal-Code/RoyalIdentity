using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Defaults;

/// <summary>
/// Detects the MP-3 capability and, only when it is absent, takes the legacy non-conditional update
/// (plan-data-operational-storage DF39). Nothing here logs the token handle (DF28).
/// </summary>
public sealed class DefaultRefreshTokenConsumer(
    IStorage storage,
    ILogger<DefaultRefreshTokenConsumer> logger) : IRefreshTokenConsumer
{
    public async Task<RefreshTokenTransition> TryConsumeAsync(
        Realm realm, RefreshToken token, DateTime consumedAt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(token);

        var store = storage.GetRefreshTokenStore(realm);

        if (store is IVersionedRefreshTokenStore versioned)
            return await versioned.TryConsumeAsync(token.Token, token.StateVersion, consumedAt, ct);

        LogMissingCapability(realm);

        // Transitional path: the read that produced `token` and this write are not one operation, so two
        // concurrent callers can both believe they won.
        if (token.ConsumedTime.HasValue)
            return RefreshTokenTransition.AlreadyConsumed(token);

        token.ConsumedTime = consumedAt;
        await store.UpdateAsync(token, ct);

        return RefreshTokenTransition.Succeeded(token);
    }

    public async Task<RefreshTokenTransition> TryUpdateAsync(
        Realm realm, RefreshToken token, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(token);

        var store = storage.GetRefreshTokenStore(realm);

        if (store is IVersionedRefreshTokenStore versioned)
            return await versioned.TryUpdateAsync(token, token.StateVersion, ct);

        LogMissingCapability(realm);

        await store.UpdateAsync(token, ct);

        return RefreshTokenTransition.Succeeded(token);
    }

    private void LogMissingCapability(Realm realm) => logger.LogDebug(
        "The refresh token store of realm {RealmId} does not provide the versioned transition capability; " +
        "using the transitional non-atomic path.",
        realm.Id);
}
