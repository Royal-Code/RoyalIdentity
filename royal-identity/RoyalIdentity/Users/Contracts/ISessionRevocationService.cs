namespace RoyalIdentity.Users.Contracts;

/// <summary>
/// Core-owned port that executes active revocation of a subject's sessions and/or refresh tokens (Q13), over the
/// session store and the refresh token store. Realm is resolved by the ambient accessor (never a parameter).
/// Synchronous now (called by the integration post-commit); a future <c>SecurityInvalidationRequested</c> event may
/// drive it. Implementations must be idempotent.
/// <para>
/// Distinct from <see cref="IUserSecurityStateProvider"/>: that port reads/validates the current security state;
/// this one executes revocation.
/// </para>
/// </summary>
public interface ISessionRevocationService
{
    /// <summary>
    /// Revokes the subject's sessions/refresh tokens per the requested <paramref name="revocation"/> scope. When the
    /// scope targets only other sessions, <paramref name="currentSessionId"/> is preserved.
    /// </summary>
    /// <param name="subjectId">The subject whose sessions/tokens are revoked.</param>
    /// <param name="revocation">Which sessions/tokens to revoke.</param>
    /// <param name="currentSessionId">The current session <c>sid</c> to preserve when revoking only other sessions, or <c>null</c>.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RevokeAsync(
        string subjectId,
        SessionRevocation revocation,
        string? currentSessionId,
        CancellationToken ct = default);
}
