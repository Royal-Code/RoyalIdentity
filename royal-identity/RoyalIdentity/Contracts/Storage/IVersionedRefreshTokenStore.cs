using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// <para>
///     Capability of an <see cref="IRefreshTokenStore"/> that can move a refresh token through conditional,
///     observable state transitions (MP-3 / plan-data-operational-storage DF12). Like MP-2 it is a separate
///     interface, never a default member of the CRUD contract: a backing that cannot honour it does not claim
///     to, the core falls back to the legacy non-atomic update explicitly (DF39) and the EF adapter must
///     implement it.
/// </para>
/// <para>
///     The post-consumption tolerance (<c>Client.RefreshTokenPostConsumedTimeTolerance</c>) is a separate
///     product policy and is not part of this primitive: a conflict is never converted into a success here,
///     and only a rematerialized consumed state may then be submitted to the tolerance by the caller.
/// </para>
/// </summary>
public interface IVersionedRefreshTokenStore
{
    /// <summary>
    /// Marks the token as consumed if — and only if — it is still unconsumed at the expected state version.
    /// Exactly one concurrent caller can observe <see cref="RefreshTokenTransitionOutcome.Succeeded"/>.
    /// </summary>
    /// <param name="token">The raw refresh-token handle.</param>
    /// <param name="expectedStateVersion">The version observed when the token was materialized.</param>
    /// <param name="consumedAt">The consumption timestamp, supplied by the composition's <see cref="TimeProvider"/>.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<RefreshTokenTransition> TryConsumeAsync(
        string token, int expectedStateVersion, DateTime consumedAt, CancellationToken ct);

    /// <summary>
    /// Persists a later change to a reusable refresh token, also conditioned on the expected state version, so
    /// a concurrent writer cannot be lost. The expected version must come from materialization, never from the
    /// instance being written.
    /// </summary>
    /// <param name="token">The token to persist.</param>
    /// <param name="expectedStateVersion">The version observed when the token was materialized.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<RefreshTokenTransition> TryUpdateAsync(RefreshToken token, int expectedStateVersion, CancellationToken ct);
}
