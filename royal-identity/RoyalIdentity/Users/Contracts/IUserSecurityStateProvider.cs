namespace RoyalIdentity.Users.Contracts;

/// <summary>
/// Optional capability port (core-owned, realm-bound at construction): reads the current security-sensitive state of
/// an account so the IdP can capture the security stamp at sign-in and enforce passive session invalidation by
/// <c>SessionsValidAfter</c> (Q15). A user provider with no such state simply does not expose this port, and the IdP
/// degrades gracefully (no stamp capture, no state enforcement) — unless the realm requires it
/// (<c>RealmOptions.Session.RequiresSecurityStateProvider</c>), which is a composition error detected fail-fast during
/// session validation.
/// <para>
/// This is distinct from <see cref="ISessionRevocationService"/>: this port <b>reads/validates</b> the current
/// security state; the revocation service <b>executes</b> active revocation of sessions/refresh tokens (Q13/Q15).
/// </para>
/// </summary>
public interface IUserSecurityStateProvider
{
    /// <summary>
    /// Gets the current security state for the subject, or <c>null</c> when the subject is unknown to the provider.
    /// </summary>
    /// <param name="subjectId">The OIDC subject identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<UserSecurityState?> GetSecurityStateAsync(string subjectId, CancellationToken ct = default);
}
