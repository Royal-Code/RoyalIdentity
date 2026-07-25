using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// <para>
///     Capability of an <see cref="IAuthorizationCodeStore"/> that can consume an authorization code
///     atomically (MP-2 / plan-data-operational-storage DF11). It is a separate interface, never a default
///     member of the CRUD contract, precisely so a backing that cannot honour it does not silently claim to:
///     the core detects its absence and takes the legacy, non-atomic path explicitly (DF39), while the EF
///     adapter is required to implement it and its registration fails validation if it does not.
/// </para>
/// <para>
///     There is no separate "administrative" removal here: <see cref="IAuthorizationCodeStore"/> keeps that,
///     and it stays idempotent.
/// </para>
/// </summary>
public interface ISingleUseAuthorizationCodeStore
{
    /// <summary>
    /// <para>
    ///     Consumes the code in a single indivisible operation: the expected client and redirect URI are part
    ///     of the condition, and the row is removed and returned to at most one concurrent caller.
    /// </para>
    /// <para>
    ///     Returns <c>null</c> for an absent code, a code already consumed, and a code whose client or
    ///     redirect URI does not match — deliberately indistinguishable, so the result is no oracle about the
    ///     binding. A request that fails the condition does not consume the code. Expiration and PKCE are
    ///     validated by the pipeline after the consumption, so a caller that won the code and then failed
    ///     those checks does not make it reusable.
    /// </para>
    /// </summary>
    /// <param name="code">The raw authorization code handle.</param>
    /// <param name="clientId">The client the code must belong to (Ordinal).</param>
    /// <param name="redirectUri">The redirect URI the code must have been issued for (Ordinal).</param>
    /// <param name="ct">The cancellation token.</param>
    Task<AuthorizationCode?> ConsumeAuthorizationCodeAsync(
        string code, string clientId, string redirectUri, CancellationToken ct);
}
