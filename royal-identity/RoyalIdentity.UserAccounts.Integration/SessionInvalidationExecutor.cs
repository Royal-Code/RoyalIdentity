using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Translates the module's per-trigger active-revocation policy (<see cref="SessionInvalidation"/>, Q7) into the
/// core <see cref="SessionRevocation"/> scope and executes it through the IdP's <see cref="ISessionRevocationService"/>
/// (Q13). This is the <c>.Integration</c>'s single bridge from "the module decided what to invalidate" to "the IdP
/// revokes it" — idempotent and intended to run post-commit. The trigger that calls this (a credential-change use case
/// or a future <c>SecurityInvalidationRequested</c> event) is wired with the account events (Fase 9) / edge endpoints.
/// </summary>
public sealed class SessionInvalidationExecutor(ISessionRevocationService revocationService)
{
    /// <summary>
    /// Executes the module invalidation policy for the subject. A no-op when the policy is
    /// <see cref="SessionInvalidation.None"/>.
    /// </summary>
    /// <param name="subjectId">The subject whose sessions/refresh tokens are revoked.</param>
    /// <param name="invalidation">The module policy resolved for the trigger.</param>
    /// <param name="currentSessionId">The current session <c>sid</c> to preserve when revoking only other sessions.</param>
    /// <param name="ct">The cancellation token.</param>
    public Task ExecuteAsync(
        string subjectId,
        SessionInvalidation invalidation,
        string? currentSessionId,
        CancellationToken ct = default)
    {
        return revocationService.RevokeAsync(subjectId, Translate(invalidation), currentSessionId, ct);
    }

    private static SessionRevocation Translate(SessionInvalidation invalidation)
    {
        var revocation = SessionRevocation.None;

        if (invalidation.HasFlag(SessionInvalidation.CurrentSession))
        {
            revocation |= SessionRevocation.CurrentSession;
        }

        if (invalidation.HasFlag(SessionInvalidation.OtherSessions))
        {
            revocation |= SessionRevocation.OtherSessions;
        }

        if (invalidation.HasFlag(SessionInvalidation.RefreshTokens))
        {
            revocation |= SessionRevocation.RefreshTokens;
        }

        return revocation;
    }
}
