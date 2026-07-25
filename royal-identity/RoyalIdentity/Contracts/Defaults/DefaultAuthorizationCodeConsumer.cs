using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Defaults;

/// <summary>
/// Detects the MP-2 capability and, only when it is absent, takes the legacy get-then-remove path
/// (plan-data-operational-storage DF39). Nothing here logs the code handle (DF28).
/// </summary>
public sealed class DefaultAuthorizationCodeConsumer(
    IStorage storage,
    ILogger<DefaultAuthorizationCodeConsumer> logger) : IAuthorizationCodeConsumer
{
    public async Task<AuthorizationCode?> ConsumeAsync(
        Realm realm, string code, string clientId, string redirectUri, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(realm);

        var store = storage.GetAuthorizationCodeStore(realm);

        if (store is ISingleUseAuthorizationCodeStore singleUse)
            return await singleUse.ConsumeAuthorizationCodeAsync(code, clientId, redirectUri, ct);

        logger.LogDebug(
            "The authorization code store of realm {RealmId} does not provide the single-use capability; " +
            "consuming through the transitional non-atomic path.",
            realm.Id);

        return await ConsumeWithoutCapabilityAsync(store, code, clientId, redirectUri, ct);
    }

    /// <summary>
    /// Transitional path for backings without the capability. It reproduces the target observable semantics —
    /// a single <c>null</c> for absent, consumed or mismatched binding, and no removal when the binding fails
    /// — but it is not atomic: two concurrent callers can both obtain the same code.
    /// </summary>
    private static async Task<AuthorizationCode?> ConsumeWithoutCapabilityAsync(
        IAuthorizationCodeStore store, string code, string clientId, string redirectUri, CancellationToken ct)
    {
        var authorizationCode = await store.GetAuthorizationCodeAsync(code, ct);

        if (authorizationCode is null)
            return null;

        if (!string.Equals(authorizationCode.ClientId, clientId, StringComparison.Ordinal))
            return null;

        if (!string.Equals(authorizationCode.RedirectUri, redirectUri, StringComparison.Ordinal))
            return null;

        await store.RemoveAuthorizationCodeAsync(code, ct);

        return authorizationCode;
    }
}
