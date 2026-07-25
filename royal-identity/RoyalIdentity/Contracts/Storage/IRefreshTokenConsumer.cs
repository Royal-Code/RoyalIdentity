using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// <para>
///     Conditional refresh-token transitions as the token flow sees them. Like
///     <see cref="IAuthorizationCodeConsumer"/>, it hides whether the backing implements
///     <see cref="IVersionedRefreshTokenStore"/>: with the capability the transition is the versioned
///     primitive of MP-3; without it — only while the in-memory fake is still the default backing — this seam
///     takes the legacy, non-atomic update path explicitly (plan-data-operational-storage DF39).
/// </para>
/// <para>
///     The fallback and this detection disappear in Plano 4. The tolerance policy is never applied here: the
///     caller decides what to do with an <see cref="RefreshTokenTransitionOutcome.AlreadyConsumed"/> result.
/// </para>
/// </summary>
public interface IRefreshTokenConsumer
{
    /// <summary>
    /// Marks the materialized token as consumed, conditioned on the state version it was materialized with.
    /// </summary>
    /// <param name="realm">The realm that owns the token.</param>
    /// <param name="token">The token as materialized by the store — the source of both handle and version.</param>
    /// <param name="consumedAt">The consumption timestamp, from the composition's <see cref="TimeProvider"/>.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<RefreshTokenTransition> TryConsumeAsync(
        Realm realm, RefreshToken token, DateTime consumedAt, CancellationToken ct = default);

    /// <summary>
    /// Persists a later change to a reusable token, conditioned on the same materialized state version.
    /// </summary>
    Task<RefreshTokenTransition> TryUpdateAsync(Realm realm, RefreshToken token, CancellationToken ct = default);
}
